import { Client } from "file:///C:/Users/Usuario/AppData/Local/npm-cache/_npx/a7204b5813574340/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js";
import { StdioClientTransport } from "file:///C:/Users/Usuario/AppData/Local/npm-cache/_npx/a7204b5813574340/node_modules/@modelcontextprotocol/sdk/dist/esm/client/stdio.js";
import path from "path";
import fs from "fs";
import { execSync } from "child_process";

async function main() {
  const projectRoot = path.resolve(import.meta.dirname, "../..");
  const distDir = path.join(projectRoot, "src", "Frontend", "dist");

  if (!fs.existsSync(distDir) || !fs.existsSync(path.join(distDir, "index.html"))) {
    console.log("Compilando Frontend antes de publicar...");
    execSync("npm run build", { cwd: path.join(projectRoot, "src", "Frontend"), stdio: "inherit" });
  }

  const timestamp = new Date().toISOString().replace(/[-:T]/g, "").slice(0, 15).replace(/^(\d{8})(\d{6})$/, "$1_$2");
  const zipPath = path.join(projectRoot, "src", "Frontend", `dist_${timestamp}.zip`);

  console.log(`Generando paquete comprimido: ${zipPath}`);
  execSync(`powershell -NoProfile -Command "Compress-Archive -Path '${distDir}\\*' -DestinationPath '${zipPath}' -Force"`);

  console.log("Conectando con Hostinger Hosting MCP...");
  const transport = new StdioClientTransport({
    command: "npx.cmd",
    args: ["--package=hostinger-api-mcp@latest", "hostinger-hosting-mcp"]
  });

  const client = new Client(
    { name: "repredisl-deployer", version: "1.0.0" },
    { capabilities: {} }
  );

  await client.connect(transport);
  console.log("Conectado con éxito al MCP de Hostinger.");

  console.log("Desplegando en pedidos.repredisl.com...");
  const result = await client.callTool({
    name: "hosting_deployStaticWebsite",
    arguments: {
      domain: "pedidos.repredisl.com",
      archivePath: zipPath,
      removeArchive: true
    }
  });

  console.log("Respuesta de Hostinger MCP:");
  console.log(JSON.stringify(result, null, 2));

  await client.close();
  console.log("¡Publicación en https://pedidos.repredisl.com completada con éxito!");
}

main().catch(err => {
  console.error("Error en la publicación:", err);
  process.exit(1);
});
