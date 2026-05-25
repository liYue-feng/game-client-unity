.PHONY: setup

# 初始化开发环境（clone 后首次运行）
setup:
	@echo ">>> 初始化开发环境..."
	git config core.hooksPath .git-hooks
	@echo ">>> Git hooks 已激活（每次提交自动包含 .claude/ 变更）"
	@echo ">>> 初始化完成"
