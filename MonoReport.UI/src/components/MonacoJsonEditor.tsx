import * as monaco from "monaco-editor";
import { useEffect, useRef } from "react";

interface Props {
  value: string;
  onChange(value: string): void;
}

export function MonacoJsonEditor({ value, onChange }: Props) {
  const host = useRef<HTMLDivElement>(null);
  const editorRef = useRef<monaco.editor.IStandaloneCodeEditor | null>(null);
  const change = useRef(onChange);
  change.current = onChange;

  useEffect(() => {
    if (!host.current) return;
    const model = monaco.editor.createModel(value, "json");
    model.updateOptions({ tabSize: 2, insertSpaces: true });
    model.detectIndentation(true, 2);
    const editor = monaco.editor.create(host.current, {
      model,
      theme: "vs",
      automaticLayout: true,
      minimap: { enabled: true },
      glyphMargin: false,
      scrollBeyondLastLine: false,
      fontFamily: "ui-monospace, SFMono-Regular, Consolas, monospace",
      fontSize: 12,
      ariaLabel: "JSON data",
      pasteAs: { enabled: false }
    });
    editorRef.current = editor;
    const subscription = editor.onDidChangeModelContent(() => change.current(editor.getValue()));
    return () => {
      editorRef.current = null;
      subscription.dispose();
      editor.dispose();
      model.dispose();
    };
  }, []);

  useEffect(() => {
    const editor = editorRef.current;
    if (editor && editor.getValue() !== value) editor.setValue(value);
  }, [value]);

  return <div className="editor" ref={host} data-testid="json-editor" />;
}
