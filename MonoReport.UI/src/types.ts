export interface Diagnostic {
  title: string;
  detail: string;
  code?: string;
  field?: string;
  part?: string;
  location?: string;
  formatter?: string;
  dataPath?: string;
}

export interface GeneratedDocument {
  blob: Blob;
  filename: string;
}

export interface TemplateFieldSwitch {
  kind: string;
  value: string;
}

export interface TemplateField {
  name: string;
  type: "string" | "array";
  formats: TemplateFieldSwitch[][];
  fields?: TemplateField[];
}

export interface TemplateAnalysis {
  valid: boolean;
  errors: Diagnostic[];
  warnings: Diagnostic[];
  fields: TemplateField[];
}
