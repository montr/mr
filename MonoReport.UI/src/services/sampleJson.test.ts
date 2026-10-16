import { describe, expect, it } from "vitest";
import type { TemplateField } from "../types";
import { fieldsToSampleJson } from "./sampleJson";

const scalar = (name: string, formats: TemplateField["formats"] = [[]]): TemplateField => ({ name, type: "string", formats });
const array = (name: string, fields: TemplateField[] = []): TemplateField => ({ name, type: "array", formats: [], fields });

describe("sample JSON conversion", () => {
  it("expands dotted paths, preserves case and order, and ignores formats", () => {
    const source = [scalar("Company.Name", [[{ kind: "general", value: "Upper" }]]), scalar("Company.Address"), scalar("company")];

    expect(fieldsToSampleJson(source)).toBe(JSON.stringify({ Company: { Name: "", Address: "" }, company: "" }, null, 2));
    expect(source[0].name).toBe("Company.Name");
    expect(source[0].formats[0][0].value).toBe("Upper");
  });

  it("creates one item for nested and empty arrays in their current scope", () => {
    expect(JSON.parse(fieldsToSampleJson([
      array("Orders", [scalar("Number"), array("Items", [scalar("Product.Name")])]),
      array("Empty")
    ]))).toEqual({ Orders: [{ Number: "", Items: [{ Product: { Name: "" } }] }], Empty: [{}] });
  });

  it("merges duplicates and resolves every shape conflict silently with array over object over string", () => {
    const forward = [
      scalar("Company"), scalar("Company.Name"),
      scalar("Mixed"), array("Mixed", [scalar("ArrayChild")]), scalar("Mixed.ObjectChild"),
      array("Nested", [scalar("Value")]), scalar("Nested.More")
    ];
    const reverse = [...forward].reverse();
    const expected = {
      Company: { Name: "" },
      Mixed: [{ ArrayChild: "", ObjectChild: "" }],
      Nested: [{ Value: "", More: "" }]
    };

    expect(JSON.parse(fieldsToSampleJson(forward))).toEqual(expected);
    expect(JSON.parse(fieldsToSampleJson(reverse))).toEqual(expected);
    expect(JSON.parse(fieldsToSampleJson([scalar("Same"), scalar("Same")]))).toEqual({ Same: "" });
  });

  it("returns an empty object for no fields", () => {
    expect(fieldsToSampleJson([])).toBe("{}");
  });

  it("uses the first recognized numeric, SpellOut, or date format as a plausible sample", () => {
    expect(JSON.parse(fieldsToSampleJson([
      scalar("Amount", [[{ kind: "general", value: "Upper" }], [{ kind: "number", value: "0.00" }]]),
      scalar("Words", [[{ kind: "general", value: "SpellOut" }]]),
      scalar("Date", [[{ kind: "date", value: "yyyy-MM-dd" }]]),
      scalar("Conflict", [[{ kind: "date", value: "yyyy" }], [{ kind: "number", value: "0" }]])
    ]))).toEqual({ Amount: 1234.5, Words: 1234.5, Date: "2026-01-15T00:00:00Z", Conflict: "2026-01-15T00:00:00Z" });
  });
});
