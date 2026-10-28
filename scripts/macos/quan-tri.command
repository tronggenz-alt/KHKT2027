#!/bin/bash
# Quản trị tài khoản (chạy trên máy chạy app): xem danh sách, tạo/cấp/bỏ admin, đặt lại mật khẩu khi người dùng quên.
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$ROOT_DIR" || exit 1
[ -x .venv/bin/python ] || { echo "LỖI: chưa cài đặt – nháy đúp setup.command trước."; read -r -p "Enter để đóng..." _; exit 1; }
.venv/bin/python -m backend.quan_tri "$@"
