import type { Plugin } from "@opencode-ai/plugin"
import { tool } from "@opencode-ai/plugin"
import { appendFileSync, mkdirSync, existsSync } from "fs"

const LOG_DIR = "/tmp/opencode-plugin-logs"
const LOG_FILE = `${LOG_DIR}/task-manager-plugin.log`

// Ensure log directory exists
if (!existsSync(LOG_DIR)) {
  try {
    mkdirSync(LOG_DIR, { recursive: true })
  } catch {
    // Silent fail
  }
}

function logToFile(message: string): void {
  try {
    const timestamp = new Date().toISOString()
    appendFileSync(LOG_FILE, `[${timestamp}] ${message}\n`)
  } catch {
    // Silent fail
  }
}

/**
 * Configuration for the Task Manager
 */
interface TaskManagerConfig {
  /** VirtualAssistant API base URL */
  apiBaseUrl: string
}

/**
 * Default configuration
 */
const defaultConfig: TaskManagerConfig = {
  apiBaseUrl: "http://localhost:5055",
}

/**
 * Load configuration from environment or use defaults
 */
function loadConfig(): TaskManagerConfig {
  return {
    apiBaseUrl: process.env.VIRTUAL_ASSISTANT_API_URL ?? defaultConfig.apiBaseUrl,
  }
}

/**
 * Task from VirtualAssistant API
 */
interface Task {
  id: number
  githubIssueUrl: string
  githubIssueNumber: number
  summary: string
  createdByAgent: string
  targetAgent: string
  status: string
  requiresApproval: boolean
  result: string | null
  createdAt: string
  approvedAt: string | null
  sentAt: string | null
  completedAt: string | null
}

/**
 * OpenCode Task Manager Plugin
 *
 * Provides task management functionality for OpenCode through VirtualAssistant.
 * Allows creating, listing, and dispatching tasks to other agents.
 */
export const TaskManagerPlugin: Plugin = async () => {
  const config = loadConfig()

  logToFile(`PLUGIN INIT: apiBaseUrl=${config.apiBaseUrl}`)

  /**
   * Create a new task
   */
  async function createTask(params: {
    githubIssueNumber: number
    githubIssueUrl?: string
    summary: string
    targetAgent: string
    requiresApproval?: boolean
  }): Promise<{ success: boolean; task?: Task; error?: string }> {
    try {
      const response = await fetch(`${config.apiBaseUrl}/api/tasks/create`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Agent-Name": "opencode",
        },
        body: JSON.stringify({
          githubIssueUrl: params.githubIssueUrl ?? `https://github.com/Olbrasoft/VirtualAssistant/issues/${params.githubIssueNumber}`,
          githubIssueNumber: params.githubIssueNumber,
          summary: params.summary,
          targetAgent: params.targetAgent,
          requiresApproval: params.requiresApproval ?? false,
        }),
        signal: AbortSignal.timeout(10000),
      })

      if (response.ok) {
        const task = (await response.json()) as Task
        logToFile(`Task created: #${task.id} - ${task.summary}`)
        return { success: true, task }
      } else {
        const errorText = await response.text()
        logToFile(`Create task error: ${response.status} - ${errorText}`)
        return { success: false, error: `HTTP ${response.status}: ${errorText}` }
      }
    } catch (error) {
      const errorMsg = error instanceof Error ? error.message : String(error)
      logToFile(`Create task exception: ${errorMsg}`)
      return { success: false, error: errorMsg }
    }
  }

  /**
   * List tasks
   */
  async function listTasks(): Promise<{ success: boolean; tasks?: Task[]; error?: string }> {
    try {
      const response = await fetch(`${config.apiBaseUrl}/api/tasks`, {
        method: "GET",
        headers: { "X-Agent-Name": "opencode" },
        signal: AbortSignal.timeout(10000),
      })

      if (response.ok) {
        const tasks = (await response.json()) as Task[]
        logToFile(`Listed ${tasks.length} tasks`)
        return { success: true, tasks }
      } else {
        const errorText = await response.text()
        logToFile(`List tasks error: ${response.status}`)
        return { success: false, error: `HTTP ${response.status}: ${errorText}` }
      }
    } catch (error) {
      const errorMsg = error instanceof Error ? error.message : String(error)
      logToFile(`List tasks exception: ${errorMsg}`)
      return { success: false, error: errorMsg }
    }
  }

  /**
   * Get pending tasks for a specific agent
   */
  async function getPendingTasks(agent: string): Promise<{ success: boolean; tasks?: Task[]; error?: string }> {
    try {
      const response = await fetch(`${config.apiBaseUrl}/api/tasks/pending/${agent}`, {
        method: "GET",
        headers: { "X-Agent-Name": "opencode" },
        signal: AbortSignal.timeout(10000),
      })

      if (response.ok) {
        const tasks = (await response.json()) as Task[]
        logToFile(`Found ${tasks.length} pending tasks for ${agent}`)
        return { success: true, tasks }
      } else {
        const errorText = await response.text()
        return { success: false, error: `HTTP ${response.status}: ${errorText}` }
      }
    } catch (error) {
      const errorMsg = error instanceof Error ? error.message : String(error)
      return { success: false, error: errorMsg }
    }
  }

  /**
   * Get task status
   */
  async function getTaskStatus(taskId: number): Promise<{ success: boolean; task?: Task; error?: string }> {
    try {
      const response = await fetch(`${config.apiBaseUrl}/api/tasks/${taskId}`, {
        method: "GET",
        headers: { "X-Agent-Name": "opencode" },
        signal: AbortSignal.timeout(10000),
      })

      if (response.ok) {
        const task = (await response.json()) as Task
        return { success: true, task }
      } else {
        const errorText = await response.text()
        return { success: false, error: `HTTP ${response.status}: ${errorText}` }
      }
    } catch (error) {
      const errorMsg = error instanceof Error ? error.message : String(error)
      return { success: false, error: errorMsg }
    }
  }

  return {
    tool: {
      /**
       * Create a new task for an agent
       */
      task_create: tool({
        description:
          "Create a new task in VirtualAssistant for another agent (usually Claude). " +
          "Use this when you need to assign work to Claude Code.",
        args: {
          githubIssueNumber: tool.schema.number().describe("GitHub issue number"),
          summary: tool.schema.string().describe("Brief summary of the task"),
          targetAgent: tool.schema.string().optional().describe("Target agent (default: claude)"),
          requiresApproval: tool.schema.boolean().optional().describe("Whether task requires approval before sending"),
        },
        async execute(args) {
          const result = await createTask({
            githubIssueNumber: args.githubIssueNumber,
            summary: args.summary,
            targetAgent: args.targetAgent ?? "claude",
            requiresApproval: args.requiresApproval ?? false,
          })

          if (result.success && result.task) {
            return `✅ Task #${result.task.id} created: "${result.task.summary}" for ${result.task.targetAgent}`
          } else {
            return `❌ Failed to create task: ${result.error}`
          }
        },
      }),

      /**
       * List all tasks
       */
      task_list: tool({
        description: "List all tasks in VirtualAssistant. Shows task ID, status, target agent, and summary.",
        args: {},
        async execute() {
          const result = await listTasks()

          if (result.success && result.tasks) {
            if (result.tasks.length === 0) {
              return "No tasks found."
            }

            const lines = result.tasks.map((t) => {
              const status = t.status.toUpperCase()
              return `#${t.id} [${status}] → ${t.targetAgent}: ${t.summary} (issue #${t.githubIssueNumber})`
            })

            return `Tasks (${result.tasks.length}):\n${lines.join("\n")}`
          } else {
            return `❌ Failed to list tasks: ${result.error}`
          }
        },
      }),

      /**
       * Get pending tasks for an agent
       */
      task_pending: tool({
        description: "Get pending tasks for a specific agent.",
        args: {
          agent: tool.schema.string().optional().describe("Agent name (default: claude)"),
        },
        async execute(args) {
          const agent = args.agent ?? "claude"
          const result = await getPendingTasks(agent)

          if (result.success && result.tasks) {
            if (result.tasks.length === 0) {
              return `No pending tasks for ${agent}.`
            }

            const lines = result.tasks.map((t) => `#${t.id}: ${t.summary} (issue #${t.githubIssueNumber})`)

            return `Pending tasks for ${agent} (${result.tasks.length}):\n${lines.join("\n")}`
          } else {
            return `❌ Failed to get pending tasks: ${result.error}`
          }
        },
      }),

      /**
       * Get status of a specific task
       */
      task_status: tool({
        description: "Get the status of a specific task by ID.",
        args: {
          taskId: tool.schema.number().describe("Task ID"),
        },
        async execute(args) {
          const result = await getTaskStatus(args.taskId)

          if (result.success && result.task) {
            const t = result.task
            return (
              `Task #${t.id}:\n` +
              `  Status: ${t.status}\n` +
              `  Issue: #${t.githubIssueNumber}\n` +
              `  Summary: ${t.summary}\n` +
              `  Target: ${t.targetAgent}\n` +
              `  Created: ${t.createdAt}\n` +
              `  Sent: ${t.sentAt ?? "not yet"}\n` +
              `  Completed: ${t.completedAt ?? "not yet"}`
            )
          } else {
            return `❌ Failed to get task status: ${result.error}`
          }
        },
      }),
    },
  }
}

// Default export for convenience
export default TaskManagerPlugin
