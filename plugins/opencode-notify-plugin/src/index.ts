import type { Plugin } from "@opencode-ai/plugin"
import { tool } from "@opencode-ai/plugin"
import { appendFileSync, mkdirSync, existsSync } from "fs"

const LOG_DIR = "/tmp/opencode-plugin-logs"
const LOG_FILE = `${LOG_DIR}/notify-plugin.log`

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
 * Configuration for the Notify service
 */
interface NotifyConfig {
  /** VirtualAssistant API endpoint for notifications */
  notifyUrl: string
}

/**
 * Default configuration
 */
const defaultConfig: NotifyConfig = {
  notifyUrl: "http://localhost:5055/api/notifications",
}

/**
 * Load configuration from environment or use defaults
 */
function loadConfig(): NotifyConfig {
  return {
    notifyUrl: process.env.OPENCODE_NOTIFY_URL ?? defaultConfig.notifyUrl,
  }
}

/**
 * Response from VirtualAssistant notify endpoint
 */
interface NotifyResponse {
  success: boolean
  message: string
  text: string
  source: string
}

/**
 * OpenCode Notify Plugin
 *
 * Provides notification functionality for OpenCode through VirtualAssistant.
 * Instead of speaking directly, this plugin notifies VirtualAssistant
 * which handles the TTS output.
 *
 * @example
 * // The AI can use the notify tool like this:
 * // notify({ text: "Úkol byl dokončen." })
 */
export const NotifyPlugin: Plugin = async () => {
  const config = loadConfig()

  logToFile(`PLUGIN INIT: notifyUrl=${config.notifyUrl}`)

  /**
   * Send notification to VirtualAssistant
   * Uses source: "opencode" for voice differentiation
   */
  async function notify(text: string, issueIds?: number[]): Promise<{ success: boolean; error?: string }> {
    try {
      const requestBody: { text: string; source: string; issueIds?: number[] } = { text, source: "opencode" }
      if (issueIds && issueIds.length > 0) {
        requestBody.issueIds = issueIds
      }
      const response = await fetch(config.notifyUrl, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(requestBody),
        signal: AbortSignal.timeout(5000), // 5 second timeout
      })

      if (response.ok) {
        const data = (await response.json()) as NotifyResponse
        logToFile(`Notification sent: ${text.substring(0, 100)}`)
        return { success: true }
      } else {
        const errorMsg = `HTTP ${response.status} ${response.statusText}`
        logToFile(`API error: ${errorMsg}`)
        return { success: false, error: errorMsg }
      }
    } catch (error) {
      const errorMsg = error instanceof Error ? error.message : String(error)
      logToFile(`Notification error: ${errorMsg}`)
      return { success: false, error: errorMsg }
    }
  }

  return {
    /**
     * Custom tools provided by this plugin
     */
    tool: {
      /**
       * Notify VirtualAssistant with text (for TTS output)
       */
      notify: tool({
        description:
          "Notify VirtualAssistant with text to speak aloud. Use this for voice confirmations, " +
          "task acknowledgments, and summaries. Text should be in Czech language, " +
          "natural and conversational. Keep it brief (1-3 sentences). " +
          "Optionally include related GitHub issue IDs for context.",
        args: {
          text: tool.schema.string().describe("The text to notify (Czech language preferred)"),
          issueIds: tool.schema.array(tool.schema.number()).optional().describe("Optional array of GitHub issue numbers related to this notification"),
        },
        async execute(args) {
          const result = await notify(args.text, args.issueIds)
          if (result.success) {
            const issueInfo = args.issueIds?.length ? ` [issues: ${args.issueIds.join(", ")}]` : ""
            return `„${args.text}"${issueInfo}`
          } else {
            return `[Notify error: ${result.error}] „${args.text}"`
          }
        },
      }),
    },
  }
}

// Default export for convenience
export default NotifyPlugin
