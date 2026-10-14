import { act, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { DocxPreview } from "./DocxPreview";

const renderAsync = vi.fn();
const normalizeDocxForPreview = vi.fn();
vi.mock("docx-preview", () => ({ renderAsync: (...args: unknown[]) => renderAsync(...args) }));
vi.mock("../services/docxPreviewNormalization", () => ({ normalizeDocxForPreview: (...args: unknown[]) => normalizeDocxForPreview(...args) }));

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((done, fail) => { resolve = done; reject = fail; });
  return { promise, resolve, reject };
}

describe("DOCX preview boundary", () => {
  beforeEach(() => {
    renderAsync.mockReset();
    normalizeDocxForPreview.mockReset();
    normalizeDocxForPreview.mockImplementation(async (source: Blob | File) => source);
  });

  it("reports a render failure without throwing away the supplied document", async () => {
    renderAsync.mockRejectedValueOnce(new Error("render detail"));
    const source = new Blob([new Uint8Array([1, 2, 3])]);
    render(<DocxPreview source={source} label="Result" />);
    expect(await screen.findByText("Result preview could not be rendered.")).toBeInTheDocument();
    expect(normalizeDocxForPreview).toHaveBeenCalledWith(source);
    expect(renderAsync).toHaveBeenCalledWith(source, expect.any(HTMLElement), undefined, expect.any(Object));
  });

  it("reports a normalization failure without invoking the renderer", async () => {
    normalizeDocxForPreview.mockRejectedValueOnce(new Error("malformed package"));
    render(<DocxPreview source={new Blob([new Uint8Array([1])])} label="Template" />);

    expect(await screen.findByText("Template preview could not be rendered.")).toBeInTheDocument();
    expect(renderAsync).not.toHaveBeenCalled();
  });

  it("never renders a normalization result superseded by source replacement", async () => {
    const firstNormalization = deferred<Blob>();
    const secondNormalization = deferred<Blob>();
    normalizeDocxForPreview.mockReturnValueOnce(firstNormalization.promise).mockReturnValueOnce(secondNormalization.promise);
    renderAsync.mockResolvedValue(undefined);
    const first = new Blob([new Uint8Array([1])]);
    const second = new Blob([new Uint8Array([2])]);
    const normalizedSecond = new Blob([new Uint8Array([4])]);
    const view = render(<DocxPreview source={first} label="Template" />);
    view.rerender(<DocxPreview source={second} label="Template" />);
    await waitFor(() => expect(normalizeDocxForPreview).toHaveBeenCalledTimes(2));

    await act(async () => firstNormalization.reject(new Error("obsolete malformed package")));
    expect(renderAsync).not.toHaveBeenCalled();
    expect(screen.queryByText("Template preview could not be rendered.")).not.toBeInTheDocument();
    await act(async () => secondNormalization.resolve(normalizedSecond));

    await waitFor(() => expect(renderAsync).toHaveBeenCalledWith(normalizedSecond, expect.any(HTMLElement), undefined, expect.any(Object)));
    expect(renderAsync).toHaveBeenCalledTimes(1);
    expect(renderAsync.mock.calls[0][0]).toBe(normalizedSecond);
  });

  it("isolates a replacement from an obsolete render host", async () => {
    renderAsync.mockReturnValue(new Promise(() => undefined));
    const first = new Blob([new Uint8Array([1])]);
    const second = new Blob([new Uint8Array([2])]);
    const view = render(<DocxPreview source={first} label="Template" />);
    await waitFor(() => expect(renderAsync).toHaveBeenCalledTimes(1));
    const firstHost = renderAsync.mock.calls.at(-1)?.[1] as HTMLElement;

    view.rerender(<DocxPreview source={second} label="Template" />);
    await waitFor(() => expect(renderAsync).toHaveBeenCalledTimes(2));

    const secondHost = renderAsync.mock.calls.at(-1)?.[1] as HTMLElement;
    expect(secondHost).not.toBe(firstHost);
    expect(firstHost.isConnected).toBe(false);
    expect(secondHost.isConnected).toBe(true);
  });
});
