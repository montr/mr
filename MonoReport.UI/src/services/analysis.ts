import type { Diagnostic, TemplateAnalysis, TemplateField, TemplateFieldSwitch } from "../types";
import { normalizeProblem } from "./generation";

export class AnalysisFailure extends Error {
  constructor(public readonly diagnostics: Diagnostic[]) {
    super("Template analysis failed");
  }
}

function text(value: unknown): string | undefined {
  return typeof value === "string" && value.trim() ? value.trim() : undefined;
}

function diagnostic(value: unknown): Diagnostic | undefined {
  if (!value || typeof value !== "object") return undefined;
  const item = value as Record<string, unknown>;
  const detail = text(item.message) ?? text(item.detail);
  if (!detail) return undefined;
  return {
    title: text(item.title) ?? text(item.code) ?? "Template issue",
    detail,
    code: text(item.code),
    field: text(item.field)
  };
}

function diagnostics(value: unknown): Diagnostic[] | undefined {
  if (!Array.isArray(value)) return undefined;
  const parsed = value.map(diagnostic);
  return parsed.every(Boolean) ? parsed as Diagnostic[] : undefined;
}

function fieldSwitch(value: unknown): TemplateFieldSwitch | undefined {
  if (!value || typeof value !== "object") return undefined;
  const item = value as Record<string, unknown>;
  if (typeof item.kind !== "string" || typeof item.value !== "string") return undefined;
  return { kind: item.kind, value: item.value };
}

function field(value: unknown): TemplateField | undefined {
  if (!value || typeof value !== "object") return undefined;
  const item = value as Record<string, unknown>;
  if (typeof item.name !== "string" || !item.name || (item.type !== "string" && item.type !== "array") || !Array.isArray(item.formats)) return undefined;
  const formats = item.formats.map(sequence => Array.isArray(sequence) ? sequence.map(fieldSwitch) : undefined);
  if (!formats.every(sequence => sequence?.every(Boolean))) return undefined;
  if (item.type === "string") {
    if (item.fields !== undefined && item.fields !== null) return undefined;
    return { name: item.name, type: item.type, formats: formats as TemplateFieldSwitch[][] };
  }
  if (!Array.isArray(item.fields)) return undefined;
  const children = item.fields.map(field);
  if (!children.every(Boolean)) return undefined;
  return { name: item.name, type: item.type, formats: formats as TemplateFieldSwitch[][], fields: children as TemplateField[] };
}

function parseAnalysis(value: unknown): TemplateAnalysis | undefined {
  if (!value || typeof value !== "object") return undefined;
  const item = value as Record<string, unknown>;
  const errors = diagnostics(item.errors);
  const warnings = diagnostics(item.warnings);
  if (typeof item.valid !== "boolean" || !errors || !warnings || !Array.isArray(item.fields)) return undefined;
  const fields = item.fields.map(field);
  if (!fields.every(Boolean)) return undefined;
  return { valid: item.valid, errors, warnings, fields: fields as TemplateField[] };
}

async function responseJson(response: Response): Promise<unknown> {
  try { return await response.json(); } catch { return undefined; }
}

export async function analyzeTemplate(file: File, signal?: AbortSignal): Promise<TemplateAnalysis> {
  const body = new FormData();
  body.append("template", file);
  let response: Response;
  try {
    response = await fetch("/api/documents/analyze", { method: "POST", body, signal });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") throw error;
    throw new AnalysisFailure([{ title: "Server unavailable", detail: "The analysis service could not be reached." }]);
  }
  const payload = await responseJson(response);
  if (!response.ok) throw new AnalysisFailure(normalizeProblem(payload, response.status));
  const analysis = parseAnalysis(payload);
  if (!analysis) throw new AnalysisFailure([{ title: "Unexpected response", detail: "The server returned an invalid template analysis." }]);
  if (!analysis.valid) {
    throw new AnalysisFailure(analysis.errors.length ? analysis.errors : [{ title: "Invalid template", detail: "The template could not be analyzed safely." }]);
  }
  return analysis;
}
