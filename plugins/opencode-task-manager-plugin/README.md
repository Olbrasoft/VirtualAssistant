# OpenCode Task Manager Plugin

Plugin for managing tasks between OpenCode and VirtualAssistant.

## Features

- `task_create` - Create a new task for another agent (usually Claude)
- `task_list` - List all tasks
- `task_pending` - Get pending tasks for an agent
- `task_status` - Get status of a specific task

## Installation

```bash
cd plugins/opencode-task-manager-plugin
npm install
npm run build
```

Then symlink to OpenCode plugins directory:
```bash
ln -sf $(pwd)/dist ~/.config/opencode/plugin/task-manager.js
```

## Usage

The plugin provides the following tools:

### task_create
Create a new task:
```
task_create({ githubIssueNumber: 204, summary: "Implement feature X", targetAgent: "claude" })
```

### task_list
List all tasks:
```
task_list()
```

### task_pending
Get pending tasks for an agent:
```
task_pending({ agent: "claude" })
```

### task_status
Get status of a specific task:
```
task_status({ taskId: 10 })
```

## Configuration

Environment variables:
- `VIRTUAL_ASSISTANT_API_URL` - API base URL (default: http://localhost:5055)
