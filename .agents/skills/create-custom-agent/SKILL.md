---
name: create-custom-agent
description: Creates VS Code custom agent files (.agent.md) for specialized AI personas with tools, instructions, and handoffs. Use when scaffolding new custom agents, configuring agent workflows, or setting up agent-to-agent handoffs.
---

# Create Custom Agent

This skill helps you create VS Code custom agent files that define specialized AI personas for development tasks. Custom agents configure which tools are available, provide specialized instructions, and can chain together via handoffs.

## When to Use

- Creating a new custom agent from scratch
- Scaffolding an `.agent.md` file with proper frontmatter
- Setting up agent-to-agent handoffs for multi-step workflows
- Configuring tool restrictions for specialized roles (planner, reviewer, etc.)
- Creating workspace-shared or user-profile agents

## When Not to Use

- Creating instruction files (use `.instructions.md` instead)
- Creating reusable prompts (use `.prompt.md` instead)
- Modifying existing agents (edit the file directly)

## Inputs

| Input | Required | Description |
|-------|----------|-------------|
| Agent name | Yes | Descriptive name for the agent (e.g., `planner`, `code-reviewer`) |
| Description | Yes | Brief description shown as placeholder text in chat |
| Purpose/Persona | Yes | What role the agent plays and how it should behave |
| Tools | Recommended | List of tools or tool sets the agent can use |
| Handoffs | Optional | Next-step agents to transition to after completing work |

## Workflow

### Step 1: Create the agent file

Create a file with `.agent.md` extension in the `agents/` directory:

```
agents/<agent-name>.agent.md
```

### Step 2: Add YAML frontmatter

Add the header with required and optional fields:

```yaml
---
name: <agent-name>
description: <brief description for chat placeholder>
tools:
  - <tool-name>
  - <tool-set-name>
---
```

#### Available frontmatter fields:

| Field | Required | Description |
|-------|----------|-------------|
| `name` | No | Display name (defaults to filename) |
| `description` | Yes | Placeholder text shown in chat input |
| `argument-hint` | No | Hint text guiding user interaction |
| `tools` | No | List of available tools/tool sets |
| `agents` | No | List of allowed subagents (`*` for all, `[]` for none) |
| `model` | No | AI model name or prioritized array of models |
| `handoffs` | No | List of next-step agent transitions |
| `user-invokable` | No | Show in agents dropdown (default: true) |
| `disable-model-invocation` | No | Prevent subagent invocation (default: false) |
| `target` | No | Target environment: `vscode` or `github-copilot` |
| `mcp-servers` | No | MCP server configs for GitHub Copilot target |

### Step 3: Configure tools

Specify which tools the agent can use:

```yaml
tools:
  - search              # Built-in tool
  - fetch               # Built-in tool
  - codebase            # Tool set
  - myServer/*          # All tools from an MCP server
```
