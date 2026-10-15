import * as monaco from "monaco-editor";
import { useEffect, useRef } from "react";

interface Props {
  value: string;
  onChange(value: string): void;
}

export function MonacoJsonEditor({ value, onChange }: Props) {
  const host = useRef<HTMLDivElement>(null);
  const change = useRef(onChange);
  change.current = onChange;

  useEffect(() => {
    if (!host.current) return;
    const editor = monaco.editor.create(host.current, {
      value,
      language: "json",
      theme: "vs",
      automaticLayout: true,
      minimap: { enabled: true },
      scrollBeyondLastLine: false,
      fontFamily: "ui-monospace, SFMono-Regular, Consolas, monospace",
      fontSize: 12,
      ariaLabel: "JSON data"
    });
    const subscription = editor.onDidChangeModelContent(() => change.current(editor.getValue()));
    return () => {
      subscription.dispose();
      editor.dispose();
    };
  }, []);

  useEffect(() => {
    const editor = monaco.editor.getEditors().find(candidate => candidate.getDomNode()?.parentElement === host.current);
    if (editor && editor.getValue() !== value) editor.setValue(value);
  }, [value]);

  return <div className="editor" ref={host} data-testid="json-editor" />;
}
