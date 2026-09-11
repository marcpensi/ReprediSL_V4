import { Client } from "file:///C:/Users/Usuario/AppData/Local/npm-cache/_npx/a7204b5813574340/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js";
import { StdioClientTransport } from "file:///C:/Users/Usuario/AppData/Local/npm-cache/_npx/a7204b5813574340/node_modules/@modelcontextprotocol/sdk/dist/esm/client/stdio.js";
import path from "path";
import fs from "fs";
import { execSync } from "child_process";

async function main() {
  const projectRoot = path.resolve(import.meta.dirname, "../..");
  const distDir = path.join(projectRoot, "src", "Frontend", "dist");

  if (!process.argv.includes("--no-build")) {
    console.log("Compilando Frontend con npm run build...");
    execSync("npm run build", { cwd: path.join(projectRoot, "src", "Frontend"), stdio: "inherit" });
  } else {
    console.log("Omitiendo compilación (--no-build detectado). Usando 'dist' actual.");
  }

  const zipName = `dist_${Date.now()}.zip`;
  const zipPath = path.join(projectRoot, "src", "Frontend", zipName);

  console.log(`Generando paquete comprimido: ${zipPath}`);
  execSync(`powershell -NoProfile -Command "Compress-Archive -Path '${distDir}\\*' -DestinationPath '${zipPath}' -Force"`);

  console.log("Conectando con Hostinger Hosting MCP...");
  const transport = new StdioClientTransport({
    command: "npx.cmd",
    args: ["-y", "--prefer-offline", "--package=hostinger-api-mcp", "hostinger-hosting-mcp"],
    env: {
      ...process.env,
      USER_AGENT: "extension;antigravity;1.3.3"
    }
  });

  const client = new Client(
    { name: "repredisl-deployer", version: "1.0.0" },
    { capabilities: {} }
  );
  client._requestTimeout = 300000;
  if (client.options) client.options.timeout = 300000;

  await client.connect(transport);
  console.log("Conectado con éxito al MCP de Hostinger.");

  console.log("Desplegando en pedidos.repredisl.com...");
  const result = await client.callTool(
    {
      name: "hosting_deployStaticWebsite",
      arguments: {
        domain: "pedidos.repredisl.com",
        archivePath: zipPath,
        removeArchive: true
      }
    },
    undefined,
    { timeout: 300000 }
  );

  console.log("Respuesta de Hostinger MCP:");
  console.log(JSON.stringify(result, null, 2));

  await client.close();

  const textResp = result?.content?.[0]?.text || "";
  if (textResp.includes('"status":"error"') || textResp.includes('"error"')) {
    throw new Error(`El despliegue falló en Hostinger: ${textResp}`);
  }

  console.log("¡Publicación en https://pedidos.repredisl.com completada con éxito!");
}

main().catch(err => {
  console.error("Error en la publicación:", err);
  process.exit(1);
});
