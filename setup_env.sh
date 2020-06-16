# This file can be used to make your life easier on Linux
# Use "source setup_env.sh" to establish a environment to work with SeeSharp
# Use "dotnet add [YOUR_PROJECT] reference $SEESHARP_EXPERIMENT_DIR" for building and extending your experiments

[[ "${BASH_SOURCE[0]}" == "${0}" ]] && { echo >&2 "Script setup_env.sh has to be sourced. Use 'source ${BASH_SOURCE[0]}' instead. Aborting"; exit 1; }

THIS_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

# Note: We use return instead of exit as we are sourced and do not want to close the parent terminal ;)
command -v dotnet >/dev/null 2>&1 || { echo >&2 "Working with SeeSharp requires 'dotnet' to be available. Aborting"; return 1; }

export PATH="${THIS_DIR}/build/src/SeeCore:${PATH:-}"
export LD_LIBRARY_PATH="${THIS_DIR}/build/src/SeeCore:${LD_LIBRARY_PATH:-}"
export SEESHARP_DIR="${THIS_DIR}"
export SEESHARP_PROJECT_DIR="${THIS_DIR}/src/SeeSharp"
export SEESHARP_EXPERIMENT_DIR="${THIS_DIR}/src/SeeSharp/Experiments"

