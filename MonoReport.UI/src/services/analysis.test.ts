import { describe, expect, it, vi } from "vitest";
import { AnalysisFailure, analyzeTemplate } from "./analysis";

const valid = { valid: true, errors: [], warnings: [], fields: [{ name: "Name", type: "string", formats: [[]] }] };

describe("analysis client", () => {
  it("posts the original file using the existing multipart contract", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(valid), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    const template = new File([new Uint8Array([80, 75])], "template.docx");

    await expect(analyzeTemplate(template)).resolves.toMatchObject(valid);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("/api/documents/analyze");
    expect(init.method).toBe("POST");
    expect((init.body as FormData).get("template")).toBe(template);
  });

  it("maps invalid templates, HTTP failures, transport failures, and malformed successes", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ valid: false, errors: [{ code: "template.invalid_region", message: "Invalid region" }], warnings: [], fields: [] }), { status: 200 })));
    await expect(analyzeTemplate(new File([], "bad.docx"))).rejects.toMatchObject<Partial<AnalysisFailure>>({ diagnostics: [{ title: "template.invalid_region", detail: "Invalid region", code: "template.invalid_region" }] });

    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ title: "Bad request", detail: "Missing template" }), { status: 400 })));
    await expect(analyzeTemplate(new File([], "bad.docx"))).rejects.toBeInstanceOf(AnalysisFailure);

    vi.stubGlobal("fetch", vi.fn().mockRejectedValueOnce(new TypeError("private network detail")));
    await expect(analyzeTemplate(new File([], "bad.docx"))).rejects.toMatchObject<Partial<AnalysisFailure>>({ diagnostics: [{ title: "Server unavailable", detail: "The analysis service could not be reached." }] });

    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ valid: true, errors: [], warnings: [], fields: [{ name: "Name", type: "number", formats: [] }] }), { status: 200 })));
    await expect(analyzeTemplate(new File([], "bad.docx"))).rejects.toMatchObject<Partial<AnalysisFailure>>({ diagnostics: [{ title: "Unexpected response", detail: "The server returned an invalid template analysis." }] });
  });
});
