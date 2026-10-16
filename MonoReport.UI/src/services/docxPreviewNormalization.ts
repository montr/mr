import JSZip from "jszip";

const docxMediaType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
const transitionalWordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
const strictWordNamespace = "http://purl.oclc.org/ooxml/wordprocessingml/main";
const wordNamespaces = [transitionalWordNamespace, strictWordNamespace];
const supportedStory = /^word\/(?:document\.xml|header\d+\.xml|footer\d+\.xml|footnotes\.xml|endnotes\.xml)$/;

function parseStory(xml: string): XMLDocument {
  if (typeof DOMParser === "undefined" || typeof XMLSerializer === "undefined" || /<!DOCTYPE/i.test(xml)) {
    throw new Error("Unsupported preview XML");
  }
  const parseableXml = xml.startsWith("\uFEFF") ? xml.slice(1) : xml;
  const document = new DOMParser().parseFromString(parseableXml, "application/xml");
  if (document.getElementsByTagName("parsererror").length > 0) throw new Error("Malformed preview XML");
  return document;
}

function unwrapParagraphSimpleFields(document: XMLDocument): boolean {
  const fields = wordNamespaces.flatMap(namespace => Array.from(document.getElementsByTagNameNS(namespace, "fldSimple")));
  let changed = false;
  for (const field of fields) {
    const parent = field.parentElement;
    if (!parent || parent.localName !== "p" || !wordNamespaces.includes(parent.namespaceURI ?? "")) continue;
    while (field.firstChild) parent.insertBefore(field.firstChild, field);
    field.remove();
    changed = true;
  }
  return changed;
}

// docx-preview 0.4.1 drops paragraph-level w:fldSimple cached children. Remove this
// normalizer after a verified upstream release preserves them in every supported story.
export async function normalizeDocxForPreview(source: Blob | File): Promise<Blob> {
  const zip = await JSZip.loadAsync(await source.arrayBuffer());
  const stories = Object.values(zip.files).filter(entry => !entry.dir && supportedStory.test(entry.name));
  for (const story of stories) {
    const document = parseStory(await story.async("string"));
    if (unwrapParagraphSimpleFields(document)) {
      zip.file(story.name, new XMLSerializer().serializeToString(document));
    }
  }
  const bytes = await zip.generateAsync({ type: "arraybuffer" });
  return new Blob([bytes], { type: source.type || docxMediaType });
}
