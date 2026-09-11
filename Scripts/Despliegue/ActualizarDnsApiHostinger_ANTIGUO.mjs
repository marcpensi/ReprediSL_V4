import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

async function getPublicIp() {
  const res = await fetch("https://api.ipify.org");
  if (!res.ok) throw new Error("No se pudo obtener la IP pública actual.");
  return (await res.text()).trim();
}

async function main() {
  console.log("Obteniendo IP pública actual...");
  const currentIp = await getPublicIp();
  console.log(`IP pública detectada: ${currentIp}`);

  console.log("Conectando con Hostinger DNS MCP...");
  const transport = new StdioClientTransport({
    command: "npx.cmd",
    args: ["--package=hostinger-api-mcp@latest", "hostinger-dns-mcp"],
    env: {
      ...process.env,
      USER_AGENT: "extension;antigravity;1.3.3"
    }
  });

  const client = new Client(
    { name: "repredisl-dns-updater", version: "1.0.0" },
    { capabilities: {} }
  );

  await client.connect(transport);

  console.log(`Actualizando registro A para api.repredisl.com -> ${currentIp}...`);
  const updatePayload = {
    domain: "repredisl.com",
    zone: [
      {
        name: "api",
        type: "A",
        records: [
          {
            content: currentIp
          }
        ],
        ttl: 300
      }
    ]
  };

  const updateResult = await client.callTool({
    name: "DNS_updateDNSRecordsV1",
    arguments: updatePayload
  });

  console.log("Respuesta Hostinger DNS:", JSON.stringify(updateResult, null, 2));
  await client.close();
  console.log(`¡DNS para api.repredisl.com actualizado con éxito a ${currentIp}!`);
}

main().catch(err => {
  console.error("Error al actualizar DNS:", err);
  process.exit(1);
});
