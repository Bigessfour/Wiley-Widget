#!/usr/bin/env node

const fs = require("fs");
const path = require("path");
const { execSync } = require("child_process");

const root = path.resolve(__dirname, "..");
const targetPkgPath = path.join(
  root,
  "node_modules",
  "@syncfusion",
  "winforms-assistant",
  "package.json"
);

function log(...args) {
  console.log("[fix-winforms-assistant-deps]", ...args);
}

if (!fs.existsSync(targetPkgPath)) {
  log("Target package not found:", targetPkgPath);
  process.exit(0);
}

try {
  const pkgJsonRaw = fs.readFileSync(targetPkgPath, "utf8");
  const pkg = JSON.parse(pkgJsonRaw);

  const desired = "zod";
  const desiredRange = "^4.0.0";

  if (!pkg.dependencies) pkg.dependencies = {};

  if (!pkg.dependencies[desired]) {
    log("Adding", desired, "to", targetPkgPath);
    pkg.dependencies[desired] = desiredRange;
    fs.writeFileSync(targetPkgPath, JSON.stringify(pkg, null, 2) + "\n", "utf8");

    // Try to install the missing dependency inside the package folder
    const installDir = path.join(root, "node_modules", "@syncfusion", "winforms-assistant");
    try {
      log("Running npm install to add", desired, "in", installDir);
      execSync(`npm install ${desired}@${desiredRange} --no-save`, {
        cwd: installDir,
        stdio: "inherit",
      });
      log("Installed", desired);
    } catch (err) {
      log("npm install failed, please run manually in:", installDir);
      log(err.message || err);
    }
  } else {
    log(desired, "already present in dependencies — no change needed");
  }
} catch (err) {
  console.error("[fix-winforms-assistant-deps] error:", err.message || err);
  process.exit(1);
}
