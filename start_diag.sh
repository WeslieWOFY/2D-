#!/bin/bash
# 带内存泄漏诊断参数启动 Unity 编辑器
UNITY="D:/Unity编辑器/2022.3.62f2c1/Editor/Unity.exe"
PROJECT="d:/Unity项目/奇幻森林岛"

echo "启动 Unity 编辑器 + 内存泄漏诊断..."
"$UNITY" -projectPath "$PROJECT" -diag-job-temp-memory-leak-validation
