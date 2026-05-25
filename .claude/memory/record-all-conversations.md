---
name: record-all-conversations
description: 所有对话都需记录到项目记忆
metadata:
  type: feedback
---

每次对话的内容（决策、进展、问题、偏好）都要保存到 .claude/memory/ 目录中，随 git 同步，确保跨会话可追溯。

**Why:** 用户明确要求，确保上下文不丢失。

**How to apply:** 每次会话结束后，将关键决策、新发现、代码变更的动机等信息写入对应的 memory 文件。不要只依赖自动记忆，要主动记录。