export interface Diagnostic {
  title: string;
  detail: string;
  code?: string;
  field?: string;
}

export interface GeneratedDocument {
  blob: Blob;
  filename: string;
}
