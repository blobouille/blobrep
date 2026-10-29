#!/bin/bash
# Recompile et affiche les erreurs de compilation de cette passe seulement (console vidée avant).
cd "$(dirname "$0")/../.."
unity command clear_console >/dev/null 2>&1
unity command --timeout 120 recompile >/dev/null 2>&1
for i in $(seq 1 60); do
  s=$(unity command recompile_status 2>&1 | tail -1)
  echo "$s" | grep -q '"compiling":true\|in_progress\|running' || break
  sleep 2
done
unity command console_status 2>&1 | tail -1 | grep -o '"compilationFailed":[a-z]*'
unity command console 2>&1 | grep -o 'Assets[^"]*error CS[^"]*' | sed 's/\\/\//g' | sort -u | head -${1:-40}
