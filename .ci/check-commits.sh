#!/usr/bin/env bash
set -euo pipefail
: "${BASE:?}" "${HEAD:?}" "${PR_AUTHOR:?}" "${1:?}"

if [[ ${PR_AUTHOR} != 'renovate[bot]' ]]; then
	exec mise exec -- committed "${BASE}..${HEAD}" --no-merge-commit
fi

revisions=$(git rev-list "${BASE}..${HEAD}")
for revision in ${revisions}; do
	while IFS= read -r verified; do
		if [[ ${revision} == "${verified}" ]]; then
			continue 2
		fi
	done <"$1"
	mise exec -- committed "${revision}" --no-merge-commit
done
