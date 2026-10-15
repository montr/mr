const tag = process.argv[2] ?? "";
const match = /^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*))*))?$/.exec(tag);

if (!match) {
  console.error(`Invalid MonoReport release tag: ${tag}`);
  process.exit(1);
}

const version = tag.slice(1);
const prerelease = match[4] ? "true" : "false";
const output = process.env.GITHUB_OUTPUT;
if (output) {
  const fs = await import("node:fs");
  fs.appendFileSync(output, `version=${version}\nprerelease=${prerelease}\n`);
} else {
  console.log(JSON.stringify({ version, prerelease: prerelease === "true" }));
}
