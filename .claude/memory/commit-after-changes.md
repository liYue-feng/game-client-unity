---
name: commit-after-changes
description: 每次代码变动后都要提交并写清楚注释
metadata:
  type: feedback
---

每次对代码文件的变动都要：
1. 将修改的文件和记忆文件一起 `git add`
2. 写好 commit message（中文，说明 WHY）
3. 执行 `git commit`
4. 每次对话结束时执行 `git push` 推送到远程

**Why:** 用户要求，确保每次变更都有清晰的版本记录，方便追溯。推送确保多设备同步。

**How to apply:** 每次完成一个功能修改或一组相关改动后，立即提交。记忆文件（.claude/memory/）的变更也要包含在同一次提交中。不要攒多个改动一起提交。对话结束前必须 push。