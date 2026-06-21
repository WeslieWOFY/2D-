@echo off
chcp 65001 >nul
echo 启动 Unity 编辑器 + 内存泄漏诊断...
"D:/Unity编辑器/2022.3.62f2c1/Editor/Unity.exe" -projectPath "d:/Unity项目/奇幻森林岛" -diag-job-temp-memory-leak-validation
