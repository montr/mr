import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DocxPreview } from "./DocxPreview";

const renderAsync = vi.fn();
vi.mock("docx-preview", () => ({ renderAsync: (...args: unknown[]) => renderAsync(...args) }));

describe("DOCX preview boundary", () => {
  it("reports a render failure without throwing away the supplied document", async () => {
    renderAsync.mockRejectedValueOnce(new Error("render detail"));
    const source = new Blob([new Uint8Array([1, 2, 3])]);
    render(<DocxPreview source={source} label="Result" />);
    expect(await screen.findByText("Result preview could not be rendered.")).toBeInTheDocument();
    expect(renderAsync).toHaveBeenCalledWith(source, expect.any(HTMLElement), undefined, expect.any(Object));
  });

  it("isolates a replacement from an obsolete render host", () => {
    renderAsync.mockReturnValue(new Promise(() => undefined));
    const first = new Blob([new Uint8Array([1])]);
    const second = new Blob([new Uint8Array([2])]);
    const view = render(<DocxPreview source={first} label="Template" />);
    const firstHost = renderAsync.mock.calls.at(-1)?.[1] as HTMLElement;

    view.rerender(<DocxPreview source={second} label="Template" />);

    const secondHost = renderAsync.mock.calls.at(-1)?.[1] as HTMLElement;
    expect(secondHost).not.toBe(firstHost);
    expect(firstHost.isConnected).toBe(false);
    expect(secondHost.isConnected).toBe(true);
  });
});
