import JSZip from "jszip";
import { describe, expect, it, vi } from "vitest";
import { normalizeDocxForPreview } from "./docxPreviewNormalization";

const transitional = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
const strict = "http://purl.oclc.org/ooxml/wordprocessingml/main";

function story(namespace: string, body: string, root = "document") {
  const content = root === "document" ? `<w:body>${body}</w:body>` : body;
  return `<?xml version="1.0" encoding="UTF-8"?><w:${root} xmlns:w="${namespace}">${content}</w:${root}>`;
}

async function fixture(): Promise<Blob> {
  const zip = new JSZip();
  zip.file("word/document.xml", story(transitional, `<w:p><w:r><w:t>Before</w:t></w:r><w:fldSimple w:instr=" MERGEFIELD Name "><w:r><w:rPr><w:b/></w:rPr><w:t>«Name»</w:t></w:r></w:fldSimple><w:r><w:t>After</w:t></w:r></w:p><w:tbl><w:tr><w:tc><w:p><w:fldSimple w:instr=" MERGEFIELD Total "><w:r><w:t>42</w:t></w:r></w:fldSimple></w:p></w:tc></w:tr></w:tbl>`));
  zip.file("word/header2.xml", story(strict, `<w:p><w:fldSimple w:instr=" MERGEFIELD Header "><w:r><w:t>Header value</w:t></w:r></w:fldSimple></w:p>`, "hdr"));
  zip.file("word/footer1.xml", story(transitional, `<w:p><w:fldSimple w:instr=" MERGEFIELD Footer "><w:r><w:t>Footer value</w:t></w:r></w:fldSimple></w:p>`, "ftr"));
  zip.file("word/footnotes.xml", story(transitional, `<w:footnote w:id="1"><w:p><w:fldSimple w:instr=" MERGEFIELD Note "><w:r><w:t>Footnote value</w:t></w:r></w:fldSimple></w:p></w:footnote>`, "footnotes"));
  zip.file("word/endnotes.xml", story(strict, `<w:endnote w:id="1"><w:p><w:fldSimple w:instr=" MERGEFIELD End "><w:r><w:t>Endnote value</w:t></w:r></w:fldSimple></w:p></w:endnote>`, "endnotes"));
  zip.file("word/media/unchanged.txt", "untouched");
  return new Blob([await zip.generateAsync({ type: "arraybuffer" })], { type: "application/vnd.openxmlformats-officedocument.wordprocessingml.document" });
}

async function bytes(blob: Blob) {
  return Array.from(new Uint8Array(await blob.arrayBuffer()));
}

describe("DOCX preview normalization", () => {
  it("unwraps cached paragraph fields across supported stories without changing the source", async () => {
    const source = await fixture();
    const original = await bytes(source);

    const normalized = await normalizeDocxForPreview(source);
    const zip = await JSZip.loadAsync(await normalized.arrayBuffer());

    for (const [part, values] of Object.entries({
      "word/document.xml": ["Before", "«Name»", "After", "42", "<w:b"],
      "word/header2.xml": ["Header value"],
      "word/footer1.xml": ["Footer value"],
      "word/footnotes.xml": ["Footnote value"],
      "word/endnotes.xml": ["Endnote value"]
    })) {
      const xml = await zip.file(part)!.async("string");
      expect(xml).not.toContain("fldSimple");
      values.forEach(value => expect(xml).toContain(value));
    }
    expect(await zip.file("word/media/unchanged.txt")!.async("string")).toBe("untouched");
    expect(await bytes(source)).toEqual(original);
    expect(normalized).not.toBe(source);
  });

  it("keeps childless cached fields empty and ignores unsupported nested simple fields", async () => {
    const zip = new JSZip();
    zip.file("word/document.xml", story(transitional, `<w:p><w:fldSimple w:instr=" MERGEFIELD Empty "/></w:p><w:p><w:r><w:fldSimple w:instr=" MERGEFIELD Nested "><w:r><w:t>Nested cached value</w:t></w:r></w:fldSimple></w:r></w:p>`));
    const source = new Blob([await zip.generateAsync({ type: "arraybuffer" })]);

    const normalized = await JSZip.loadAsync(await (await normalizeDocxForPreview(source)).arrayBuffer());
    const xml = await normalized.file("word/document.xml")!.async("string");

    expect(xml).not.toContain("MERGEFIELD Empty");
    expect(xml).toContain("MERGEFIELD Nested");
    expect(xml).toContain("Nested cached value");
  });

  it("removes one leading UTF-8 BOM before parsing a story", async () => {
    const xml = story(transitional, `<w:p><w:fldSimple w:instr=" MERGEFIELD Name "><w:r><w:t>«Name»</w:t></w:r></w:fldSimple></w:p>`);
    const encoded = new TextEncoder().encode(xml);
    const bomEncoded = new Uint8Array(encoded.length + 3);
    bomEncoded.set([0xef, 0xbb, 0xbf]);
    bomEncoded.set(encoded, 3);
    const zip = new JSZip();
    zip.file("word/document.xml", bomEncoded);
    const source = new Blob([await zip.generateAsync({ type: "arraybuffer" })]);
    const parseFromString = vi.spyOn(DOMParser.prototype, "parseFromString");

    const normalized = await JSZip.loadAsync(await (await normalizeDocxForPreview(source)).arrayBuffer());

    expect(parseFromString).toHaveBeenCalledWith(xml, "application/xml");
    expect(await normalized.file("word/document.xml")!.async("string")).not.toContain("fldSimple");
  });

  it("rejects malformed packages and malformed supported XML", async () => {
    await expect(normalizeDocxForPreview(new Blob(["not a zip"]))).rejects.toBeDefined();
    const zip = new JSZip();
    zip.file("word/document.xml", "<w:document><broken></w:document>");
    const malformed = new Blob([await zip.generateAsync({ type: "arraybuffer" })]);
    await expect(normalizeDocxForPreview(malformed)).rejects.toBeDefined();
  });
});
