import { describe, expect, it, vi } from "vitest";
import { generateDocument, GenerationFailure, normalizeProblem } from "./generation";

const mime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

describe("generation client", () => {
  it("sends the exact multipart contract and returns actual DOCX bytes with a safe filename", async () => {
    const bytes = new Uint8Array([80, 75, 3, 4]);
    const responseBlob = new Blob([bytes], { type: mime });
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 200, blob: async () => responseBlob, headers: new Headers({ "Content-Type": mime, "Content-Disposition": "attachment; filename=document.docx" }) } as Response);
    vi.stubGlobal("fetch", fetchMock);
    const template = new File([bytes], "template.docx", { type: mime });

    const result = await generateDocument(template, "{\"value\":1}");

    expect(result.filename).toBe("document.docx");
    expect(result.blob).toBe(responseBlob);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("/api/documents/generate");
    expect(init.method).toBe("POST");
    expect(init.headers).toBeUndefined();
    const body = init.body as FormData;
    expect(body.get("template")).toBe(template);
    expect(body.get("data")).toBe("{\"value\":1}");
    expect(body.has("culture")).toBe(false);
  });

  it("preserves API Problem Details and validation-style collections without inventing issues", () => {
    expect(normalizeProblem({ title: "Document generation failed", detail: "Data is incompatible.", code: "generation.invalid_data", field: "Items" }, 422)).toEqual([
      { title: "Document generation failed", detail: "Data is incompatible.", code: "generation.invalid_data", field: "Items" }
    ]);
    expect(normalizeProblem({ errors: [{ code: "one", message: "First" }, { code: "two", detail: "Second" }] }, 422)).toHaveLength(2);
  });

  it("uses safe fallbacks for malformed problems and unsafe filenames", async () => {
    expect(normalizeProblem("<html>failure</html>", 500)).toEqual([{ title: "Request failed", detail: "The server returned HTTP 500." }]);
    const responseBlob = new Blob([new Uint8Array([1])], { type: mime });
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, status: 200, blob: async () => responseBlob, headers: new Headers({ "Content-Type": mime, "Content-Disposition": "attachment; filename=../secret.docx" }) } as Response));
    const result = await generateDocument(new File([new Uint8Array([1])], "template.docx"), "{}");
    expect(result.filename).toBe("document.docx");
  });

  it("maps unavailable transport and non-DOCX success to useful diagnostics", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("network secret")));
    await expect(generateDocument(new File([new Uint8Array([1])], "template.docx"), "{}")).rejects.toMatchObject<Partial<GenerationFailure>>({
      diagnostics: [{ title: "Server unavailable", detail: "The generation service could not be reached." }]
    });
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("html", { status: 200, headers: { "Content-Type": "text/html" } })));
    await expect(generateDocument(new File([new Uint8Array([1])], "template.docx"), "{}")).rejects.toBeInstanceOf(GenerationFailure);
  });
});
