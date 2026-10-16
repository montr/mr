import { render } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

const monaco = vi.hoisted(() => {
  const subscription = { dispose: vi.fn() };
  const model = { updateOptions: vi.fn(), detectIndentation: vi.fn(), dispose: vi.fn() };
  const editor = {
    getValue: vi.fn(() => "{\n  \"value\": 1\n}"),
    setValue: vi.fn(),
    onDidChangeModelContent: vi.fn((_listener: () => void) => subscription),
    dispose: vi.fn()
  };
  return {
    subscription,
    model,
    editor,
    createModel: vi.fn(() => model),
    create: vi.fn(() => editor)
  };
});

vi.mock("monaco-editor", () => ({
  editor: {
    createModel: monaco.createModel,
    create: monaco.create
  }
}));

import { MonacoJsonEditor } from "./MonacoJsonEditor";

describe("Monaco JSON editor", () => {
  it("creates an owned JSON model with detected two-space indentation and preserved editor options", () => {
    render(<MonacoJsonEditor value={'{\n    "value": 1\n}'} onChange={() => undefined} />);

    expect(monaco.createModel).toHaveBeenCalledWith('{\n    "value": 1\n}', "json");
    expect(monaco.model.updateOptions).toHaveBeenCalledWith({ tabSize: 2, insertSpaces: true });
    expect(monaco.model.detectIndentation).toHaveBeenCalledWith(true, 2);
    expect(monaco.create).toHaveBeenCalledWith(expect.any(HTMLDivElement), expect.objectContaining({
      model: monaco.model,
      glyphMargin: false,
      pasteAs: { enabled: false }
    }));
  });

  it("uses the local editor for value updates and disposes only its owned resources", () => {
    const onChange = vi.fn();
    const view = render(<MonacoJsonEditor value={'{\n  "value": 1\n}'} onChange={onChange} />);
    const listener = monaco.editor.onDidChangeModelContent.mock.calls[0][0];

    listener();
    expect(onChange).toHaveBeenCalledWith('{\n  "value": 1\n}');

    view.rerender(<MonacoJsonEditor value={'{\n  "value": 2\n}'} onChange={onChange} />);
    expect(monaco.editor.setValue).toHaveBeenCalledWith('{\n  "value": 2\n}');

    view.unmount();
    expect(monaco.subscription.dispose).toHaveBeenCalledOnce();
    expect(monaco.editor.dispose).toHaveBeenCalledOnce();
    expect(monaco.model.dispose).toHaveBeenCalledOnce();
  });
});
