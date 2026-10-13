import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Splitter } from "./Splitter";

describe("responsive splitter", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("keeps CSS dimensions and accessible values synchronized across breakpoints", () => {
    let mobile = false;
    let listener: (() => void) | undefined;
    vi.stubGlobal("matchMedia", vi.fn(() => ({
      get matches() { return mobile; },
      media: "(max-width: 850px)",
      onchange: null,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: (_event: string, callback: () => void) => { listener = callback; },
      removeEventListener: () => undefined,
      dispatchEvent: () => false
    })));

    render(<Splitter />);
    const splitter = screen.getByRole("separator");
    expect(splitter).toHaveAttribute("aria-orientation", "vertical");
    expect(splitter).toHaveAttribute("aria-valuenow", "45");
    fireEvent.keyDown(splitter, { key: "ArrowRight" });
    expect(splitter).toHaveAttribute("aria-valuenow", "48");
    expect(document.documentElement.style.getPropertyValue("--left-width")).toBe("48%");

    mobile = true;
    act(() => listener?.());
    expect(splitter).toHaveAttribute("aria-orientation", "horizontal");
    expect(splitter).toHaveAttribute("aria-valuenow", "50");
    expect(document.documentElement.style.getPropertyValue("--source-height")).toBe("50%");
    fireEvent.keyDown(splitter, { key: "ArrowDown" });
    expect(splitter).toHaveAttribute("aria-valuenow", "53");
    expect(document.documentElement.style.getPropertyValue("--source-height")).toBe("53%");

    mobile = false;
    act(() => listener?.());
    expect(splitter).toHaveAttribute("aria-orientation", "vertical");
    expect(splitter).toHaveAttribute("aria-valuenow", "45");
    expect(document.documentElement.style.getPropertyValue("--left-width")).toBe("45%");
  });
});
