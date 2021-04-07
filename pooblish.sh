# The "easier" publish script, which also forces dotnet test (yes, good)

set -e

dotnet test
cd contentapi && sh publish.sh "$1"