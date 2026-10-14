import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import App from "./App";

vi.mock("./components/MonacoJsonEditor", () => ({
  MonacoJsonEditor: ({ value, onChange }: { value: string; onChange(value: string): void }) => <textarea aria-label="JSON data" value={value} onChange={event => onChange(event.target.value)} />
}));

const previewSources: Array<Blob | File> = [];
vi.mock("./components/DocxPreview", () => ({
  DocxPreview: ({ source, label }: { source: Blob | File; label: string }) => {
    previewSources.push(source);
    return <div>{label} preview</div>;
  }
}));

const mime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
const template = () => new File([new Uint8Array([80, 75, 3, 4])], "sample.template.docx", { type: mime });
const success = (bytes = new Uint8Array([80, 75, 7, 8])) => {
  const blob = new Blob([bytes], { type: mime });
  return { ok: true, status: 200, blob: async () => blob, headers: new Headers({ "Content-Type": mime, "Content-Disposition": "attachment; filename=generated.docx" }) } as Response;
};

async function selectTemplate(user: ReturnType<typeof userEvent.setup>, file = template()) {
  await user.upload(screen.getByLabelText("Template and input data").querySelector("input[type=file]") as HTMLInputElement, file);
}

describe("MonoReport playground", () => {
  beforeEach(() => { previewSources.length = 0; vi.stubGlobal("fetch", vi.fn()); });

  it("starts with the approved controls, empty template, JSON document, and accessible splitter", () => {
    render(<App />);
    expect(screen.getByText("Select a template")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Form" })).toBeDisabled();
    expect(screen.getByLabelText("JSON data")).toHaveValue("{}");
    expect(screen.getByRole("button", { name: /Download/ })).toBeDisabled();
    const repositoryLink = screen.getByRole("link", { name: "MonoReport" });
    expect(repositoryLink).toHaveAttribute("href", "https://github.com/montr/monoreport");
    expect(repositoryLink.querySelector("svg")).not.toBeNull();
    const splitter = screen.getByRole("separator");
    fireEvent.keyDown(splitter, { key: "ArrowRight" });
    expect(splitter).toHaveAttribute("aria-valuenow", "48");
  });

  it("formats and copies valid JSON while invalid JSON remains unchanged and never requests", async () => {
    const user = userEvent.setup();
    render(<App />);
    const editor = screen.getByLabelText("JSON data");
    const clipboard = vi.spyOn(navigator.clipboard, "writeText");
    fireEvent.change(editor, { target: { value: '{"a":1}' } });
    await user.click(screen.getByRole("button", { name: "Format" }));
    expect(editor).toHaveValue('{\n  "a": 1\n}');
    await user.click(screen.getByRole("button", { name: "Copy" }));
    expect(clipboard).toHaveBeenCalledWith('{\n  "a": 1\n}');
    fireEvent.change(editor, { target: { value: "{" } });
    await user.click(screen.getByRole("button", { name: "Format" }));
    expect(editor).toHaveValue("{");
    expect(screen.getByText("Invalid JSON")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /Generate/ }));
    expect(fetch).not.toHaveBeenCalled();
  });

  it("uses picker and drop through one replacement path, preserves JSON, and rejects unsupported files without replacement", async () => {
    const user = userEvent.setup();
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(success()));
    render(<App />);
    const editor = screen.getByLabelText("JSON data");
    fireEvent.change(editor, { target: { value: '{"kept":true}' } });
    await selectTemplate(user);
    expect(screen.getByText("sample.template.docx")).toBeInTheDocument();
    const replacement = new File([new Uint8Array([1])], "second.docx", { type: mime });
    fireEvent.drop(screen.getByLabelText("Preview").querySelector(".canvas")!, { dataTransfer: { files: [replacement], types: ["Files"] } });
    expect(screen.getByText("second.docx")).toBeInTheDocument();
    expect(editor).toHaveValue('{"kept":true}');
    await user.click(screen.getByRole("button", { name: /Generate/ }));
    await screen.findByText("Result preview");
    expect(screen.getByRole("button", { name: /Download/ })).toBeEnabled();
    fireEvent.drop(screen.getByLabelText("Preview").querySelector(".canvas")!, { dataTransfer: { files: [new File([new Uint8Array([1])], "bad.pdf")], types: ["Files"] } });
    expect(screen.getByText("Unsupported template format")).toBeInTheDocument();
    expect(screen.getByText("second.docx")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Download/ })).toBeEnabled();
    const third = new File([new Uint8Array([2])], "third.docx", { type: mime });
    fireEvent.drop(screen.getByLabelText("Preview").querySelector(".canvas")!, { dataTransfer: { files: [third], types: ["Files"] } });
    expect(screen.getByText("third.docx")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Download/ })).toBeDisabled();
    expect(editor).toHaveValue('{"kept":true}');
  });

  it("sends generation only on click, prevents duplicates, previews real response bytes, and downloads them", async () => {
    const user = userEvent.setup();
    let resolve!: (response: Response) => void;
    const responseBlob = new Blob([new Uint8Array([80, 75, 7, 8])], { type: mime });
    const fetchMock = vi.fn(() => new Promise<Response>(done => { resolve = done; }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    await selectTemplate(user);
    expect(fetchMock).not.toHaveBeenCalled();
    const generate = screen.getByRole("button", { name: /Generate/ });
    await user.click(generate);
    expect(generate).toBeDisabled();
    await user.click(generate);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    resolve({ ok: true, status: 200, blob: async () => responseBlob, headers: new Headers({ "Content-Type": mime, "Content-Disposition": "attachment; filename=generated.docx" }) } as Response);
    await screen.findByText("Result preview");
    expect(previewSources.at(-1)).toBe(responseBlob);
    const download = screen.getByRole("button", { name: /Download/ });
    expect(download).toBeEnabled();
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => undefined);
    await user.click(download);
    expect(URL.createObjectURL).toHaveBeenCalledWith(responseBlob);
    expect(click).toHaveBeenCalled();
  });

  it("renders real API diagnostics and multiple issues without mock content", async () => {
    const user = userEvent.setup();
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({ errors: [{ code: "first", message: "First issue" }, { code: "second", detail: "Second issue" }] }), { status: 422, headers: { "Content-Type": "application/problem+json" } })));
    render(<App />);
    await selectTemplate(user);
    await user.click(screen.getByRole("button", { name: /Generate/ }));
    expect(await screen.findByText("2 errors")).toBeInTheDocument();
    expect(screen.getByText(/First issue/)).toBeInTheDocument();
    expect(screen.getByText(/Second issue/)).toBeInTheDocument();
    expect(screen.queryByText("Unmatched collection region")).not.toBeInTheDocument();
  });

  it("invalidates a successful result on JSON or template change and after failure", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn().mockResolvedValueOnce(success()).mockResolvedValueOnce(new Response(JSON.stringify({ title: "Failed", detail: "Incompatible", code: "generation.invalid_data" }), { status: 422, headers: { "Content-Type": "application/problem+json" } }));
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    await selectTemplate(user);
    await user.click(screen.getByRole("button", { name: /Generate/ }));
    await screen.findByText("Result preview");
    expect(screen.getByRole("button", { name: /Download/ })).toBeEnabled();
    await user.type(screen.getByLabelText("JSON data"), " ");
    expect(screen.getByRole("button", { name: /Download/ })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Result" })).toBeDisabled();
    await user.click(screen.getByRole("button", { name: /Generate/ }));
    expect(await screen.findByText(/generation.invalid_data/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Download/ })).toBeDisabled();
  });

  it("ignores an obsolete success after JSON changes during generation", async () => {
    const user = userEvent.setup();
    let resolve!: (response: Response) => void;
    vi.stubGlobal("fetch", vi.fn(() => new Promise<Response>(done => { resolve = done; })));
    render(<App />);
    await selectTemplate(user);
    await user.click(screen.getByRole("button", { name: /Generate/ }));
    await user.type(screen.getByLabelText("JSON data"), " ");
    resolve(success());
    await waitFor(() => expect(screen.getByRole("button", { name: "Result" })).toBeDisabled());
    expect(screen.getByRole("button", { name: /Download/ })).toBeDisabled();
  });
});
