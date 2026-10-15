import type { TemplateField } from "../types";

type Shape = "string" | "object" | "array";

interface Node {
  shape: Shape;
  children: Map<string, Node>;
}

const rank: Record<Shape, number> = { string: 0, object: 1, array: 2 };

function mergeShape(node: Node, shape: Shape) {
  if (rank[shape] > rank[node.shape]) node.shape = shape;
}

function addPath(scope: Map<string, Node>, path: string[], shape: Shape): Node {
  let children = scope;
  let node: Node | undefined;
  path.forEach((name, index) => {
    const needed = index === path.length - 1 ? shape : "object";
    node = children.get(name);
    if (!node) {
      node = { shape: needed, children: new Map() };
      children.set(name, node);
    } else {
      mergeShape(node, needed);
    }
    children = node.children;
  });
  return node!;
}

function addFields(scope: Map<string, Node>, fields: readonly TemplateField[]) {
  for (const field of fields) {
    const node = addPath(scope, field.name.split("."), field.type);
    if (field.type === "array") addFields(node.children, field.fields ?? []);
  }
}

function value(node: Node): unknown {
  if (node.shape === "string") return "";
  const object = Object.fromEntries(Array.from(node.children, ([name, child]) => [name, value(child)]));
  return node.shape === "array" ? [object] : object;
}

export function fieldsToSampleJson(fields: readonly TemplateField[]): string {
  const root = new Map<string, Node>();
  addFields(root, fields);
  return JSON.stringify(Object.fromEntries(Array.from(root, ([name, node]) => [name, value(node)])), null, 2);
}
