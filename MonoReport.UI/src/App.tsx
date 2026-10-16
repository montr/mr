import { Button, ConfigProvider, Spin } from "antd";
import { ChangeEvent, DragEvent, useEffect, useRef, useState } from "react";
import { DocxPreview } from "./components/DocxPreview";
import { MonacoJsonEditor } from "./components/MonacoJsonEditor";
import { Splitter } from "./components/Splitter";
import { analyzeTemplate, AnalysisFailure } from "./services/analysis";
import { generateDocument, GenerationFailure } from "./services/generation";
import { fieldsToSampleJson } from "./services/sampleJson";
import type { Diagnostic, GeneratedDocument } from "./types";
import "./styles.css";
import "./mobile.css";

type Tab = "template" | "result" | "errors";
type Operation = "generate" | "analyze";
const repositoryUrl = "https://github.com/montr/monoreport";

function diagnostic(title: string, detail: string): Diagnostic[] {
  return [{ title, detail }];
}

function DownloadIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3v12m-5-5 5 5 5-5M4 17v3h16v-3" /></svg>;
}

function GitHubIcon() {
  return <svg viewBox="0 0 24 24" width="13" height="13" style={{ marginRight: 5, verticalAlign: "middle" }} aria-hidden="true"><path fill="currentColor" stroke="none" d="M12 .8A11.2 11.2 0 0 0 8.46 22.62c.56.1.77-.24.77-.54v-2.13c-3.13.68-3.79-1.33-3.79-1.33-.5-1.27-1.24-1.61-1.24-1.61-1.02-.7.08-.69.08-.69 1.13.08 1.73 1.16 1.73 1.16 1 .1.78 1.4 3.27.95.1-.72.39-1.21.71-1.49-2.5-.29-5.13-1.25-5.13-5.54 0-1.23.44-2.23 1.16-3.02-.12-.28-.5-1.43.11-2.97 0 0 .94-.3 3.08 1.15a10.7 10.7 0 0 1 5.6 0c2.14-1.45 3.08-1.15 3.08-1.15.61 1.54.23 2.69.11 2.97.72.79 1.15 1.79 1.15 3.02 0 4.3-2.63 5.25-5.14 5.54.4.35.75 1.03.75 2.08v3.09c0 .3.21.65.78.54A11.2 11.2 0 0 0 12 .8Z" /></svg>;
}

function DocumentationIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 6.3c-3.8-2-7-1.7-9-1.1v13.5c3-.9 6.2-.7 9 1 2.8-1.7 6-1.9 9-1V5.2c-2-.6-5.2-.9-9 1.1Zm0 0v13.4" /></svg>;
}

function DocumentEmpty({ children }: { children: string }) {
  return <div className="empty"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" /><path d="M14 2v6h6M8 13h8M8 17h8" /></svg><strong>{children}</strong></div>;
}

export default function App() {
  const [template, setTemplate] = useState<File>();
  const [json, setJson] = useState("{}");
  const [result, setResult] = useState<GeneratedDocument>();
  const [errors, setErrors] = useState<Diagnostic[]>([]);
  const [active, setActive] = useState<Tab>("template");
  const [pending, setPending] = useState<Operation>();
  const [copyLabel, setCopyLabel] = useState("Copy");
  const [dragging, setDragging] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);
  const revision = useRef(0);
  const request = useRef<AbortController | undefined>(undefined);
  const currentTemplate = useRef<File | undefined>(undefined);
  const currentJson = useRef(json);
  const copyTimer = useRef<number | undefined>(undefined);

  useEffect(() => () => {
    revision.current += 1;
    request.current?.abort();
    if (copyTimer.current) window.clearTimeout(copyTimer.current);
  }, []);

  function invalidateResult(nextTab: Tab = "template") {
    revision.current += 1;
    request.current?.abort();
    request.current = undefined;
    setPending(undefined);
    setResult(undefined);
    setActive(current => current === "result" ? nextTab : current);
  }

  function acceptTemplate(file: File) {
    if (!/\.docx$/i.test(file.name)) {
      setErrors(diagnostic("Unsupported template format", "Select a .docx file."));
      setActive("errors");
      return;
    }
    invalidateResult("template");
    currentTemplate.current = file;
    setTemplate(file);
    setErrors([]);
    setActive("template");
  }

  function selected(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (file) acceptTemplate(file);
    event.target.value = "";
  }

  function drop(event: DragEvent) {
    event.preventDefault();
    setDragging(false);
    const file = event.dataTransfer.files?.[0];
    if (file) acceptTemplate(file);
  }

  function changeJson(value: string) {
    invalidateResult("template");
    currentJson.current = value;
    setJson(value);
  }

  async function copyJson() {
    try {
      await navigator.clipboard.writeText(json);
      setCopyLabel("Copied");
      if (copyTimer.current) window.clearTimeout(copyTimer.current);
      copyTimer.current = window.setTimeout(() => setCopyLabel("Copy"), 1200);
    } catch {
      setErrors(diagnostic("Clipboard unavailable", "The JSON could not be copied to the clipboard."));
      setActive("errors");
    }
  }

  function formatJson() {
    try {
      changeJson(JSON.stringify(JSON.parse(json), null, 2));
      setErrors([]);
    } catch {
      setErrors(diagnostic("Invalid JSON", "Fix the JSON syntax before formatting."));
      setActive("errors");
    }
  }

  async function generate() {
    if (pending) return;
    if (!template) {
      setErrors(diagnostic("Template is required", "Select a DOCX file before generating a document."));
      setActive("errors");
      return;
    }
    try {
      JSON.parse(json);
    } catch {
      invalidateResult("errors");
      setErrors(diagnostic("Invalid JSON", "Fix the JSON syntax before generating a document."));
      setActive("errors");
      return;
    }

    invalidateResult("template");
    const operation = revision.current;
    const controller = new AbortController();
    request.current = controller;
    setErrors([]);
    setPending("generate");
    try {
      const generated = await generateDocument(template, json, controller.signal);
      if (revision.current !== operation) return;
      setResult(generated);
      setActive("result");
    } catch (error) {
      if (revision.current !== operation || (error instanceof DOMException && error.name === "AbortError")) return;
      setErrors(error instanceof GenerationFailure ? error.diagnostics : diagnostic("Generation failed", "The document could not be generated."));
      setActive("errors");
    } finally {
      if (revision.current === operation) {
        request.current = undefined;
        setPending(undefined);
      }
    }
  }

  async function generateJson() {
    if (pending || !template) return;
    revision.current += 1;
    request.current?.abort();
    const operation = revision.current;
    const source = template;
    const controller = new AbortController();
    request.current = controller;
    setErrors([]);
    setPending("analyze");
    try {
      const analysis = await analyzeTemplate(source, controller.signal);
      if (revision.current !== operation || currentTemplate.current !== source) return;
      const proposed = fieldsToSampleJson(analysis.fields);
      const previous = currentJson.current;
      if (proposed === previous) return;
      if (previous.length > 0 && !window.confirm("Replace current JSON with generated example?")) return;
      if (revision.current !== operation || currentTemplate.current !== source || currentJson.current !== previous) return;
      changeJson(proposed);
      setErrors([]);
    } catch (error) {
      if (revision.current !== operation || (error instanceof DOMException && error.name === "AbortError")) return;
      setErrors(error instanceof AnalysisFailure ? error.diagnostics : diagnostic("Analysis failed", "The template could not be analyzed."));
      setActive("errors");
    } finally {
      if (revision.current === operation) {
        request.current = undefined;
        setPending(undefined);
      }
    }
  }

  function clearTemplate() {
    invalidateResult("template");
    currentTemplate.current = undefined;
    setTemplate(undefined);
    setErrors([]);
    setActive("template");
  }

  function download() {
    if (!result) return;
    const url = URL.createObjectURL(result.blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = result.filename;
    anchor.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 0);
  }

  const content = active === "errors" ? (
    errors.length ? <div className="issues"><div className="issues-title"><strong>{errors.length} {errors.length === 1 ? "error" : "errors"}</strong></div>{errors.map((error, index) => <div className="issue" key={`${error.code ?? error.title}-${index}`}><span>{index + 1}.</span><div><strong>{error.title}</strong><p>{[error.code, error.field, error.dataPath, error.location, error.formatter, error.detail].filter(Boolean).join(" · ")}</p></div></div>)}</div> : <DocumentEmpty>No errors</DocumentEmpty>
  ) : active === "result" && result ? (
    <DocxPreview source={result.blob} label="Result" />
  ) : template ? (
    <DocxPreview source={template} label="Template" />
  ) : <DocumentEmpty>Select a template</DocumentEmpty>;

  return <ConfigProvider theme={{ token: { colorPrimary: "#cf4d48", borderRadius: 6, fontSize: 13 } }}>
    <div className="app">
      <header><span className="brand"><b>mono</b>report</span><span className="subbrand">Playground</span></header>
      <main className="layout">
        <section className="source" aria-label="Template and input data">
          <div className="filebar">
            <span className="label">Template</span>
            <Button onClick={() => fileInput.current?.click()}>Select</Button>
            <span className="filename">{template?.name ?? "No file selected"}</span>
            {template && <Button type="text" className="clear" aria-label="Remove template" onClick={clearTemplate}>×</Button>}
            <Button type="primary" className="generate" aria-label="Generate document" loading={pending === "generate"} disabled={Boolean(pending)} onClick={() => void generate()}>Generate <span aria-hidden="true">→</span></Button>
            <input ref={fileInput} type="file" accept=".docx,application/vnd.openxmlformats-officedocument.wordprocessingml.document" onChange={selected} />
          </div>
          <div className="editbar">
            <div className="mode" aria-label="Input mode"><Button disabled>Form</Button><Button className="active" aria-current="true">JSON</Button></div>
            <div className="edit-actions"><Button size="small" aria-label="Generate JSON" loading={pending === "analyze"} disabled={!template || Boolean(pending)} onClick={() => void generateJson()}>Generate JSON</Button><Button size="small" onClick={() => void copyJson()}>{copyLabel}</Button><Button size="small" onClick={formatJson}>Format</Button></div>
          </div>
          <MonacoJsonEditor value={json} onChange={changeJson} />
        </section>
        <Splitter />
        <section className="preview" aria-label="Preview">
          <div className="previewbar">
            <nav className="tabs" aria-label="Preview tabs">
              <button className={active === "template" ? "active" : ""} onClick={() => setActive("template")}>Template</button>
              <button className={active === "result" ? "active" : ""} disabled={!result} onClick={() => result && setActive("result")}>Result</button>
              <button className={active === "errors" ? "active" : ""} onClick={() => setActive("errors")}>Errors {errors.length > 0 && <span className="count">{errors.length}</span>}</button>
            </nav>
            <span className="spacer" />
            {pending && <Spin size="small" aria-label={pending === "analyze" ? "Analyzing template" : "Generating document"} />}
            <Button className="download" disabled={!result} onClick={download} icon={<DownloadIcon />}>Download</Button>
          </div>
          <div
            className={`canvas ${active === "errors" && errors.length > 0 ? "errors-view" : ""} ${dragging ? "drop-target" : ""}`}
            onDragEnter={event => { if (event.dataTransfer.types.includes("Files")) { event.preventDefault(); setDragging(true); } }}
            onDragOver={event => { if (event.dataTransfer.types.includes("Files")) event.preventDefault(); }}
            onDragLeave={event => { if (!event.currentTarget.contains(event.relatedTarget as Node)) setDragging(false); }}
            onDrop={drop}
          >{content}</div>
        </section>
      </main>
      <footer>
        <span className="project-links"><a href={repositoryUrl} target="_blank" rel="noreferrer" title="MonoReport on GitHub"><GitHubIcon />MonoReport</a>{__APP_VERSION__ === "dev" ? <span>dev</span> : <a href={`${repositoryUrl}/releases/tag/v${__APP_VERSION__}`} target="_blank" rel="noreferrer">v{__APP_VERSION__}</a>}</span>
        <a href="/docs/" title="API documentation"><DocumentationIcon />API docs</a>
      </footer>
    </div>
  </ConfigProvider>;
}
