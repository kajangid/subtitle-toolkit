#!/usr/bin/env bash
set -e

CSPROJ="src/SubtitleToolkit/SubtitleToolkit.csproj"

# Extract current version (stripping any prerelease suffixes)
CURRENT_VER=$(grep -oPm1 "(?<=<Version>)[^<]+" "$CSPROJ" | cut -d'-' -f1)

if [ -z "$CURRENT_VER" ]; then
    echo "Error: Could not extract current version from $CSPROJ"
    exit 1
fi

IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT_VER"
TYPE="${1:-patch}"

case "$TYPE" in
    patch)
        PATCH=$((PATCH + 1))
        NEW_VER="${MAJOR}.${MINOR}.${PATCH}"
        ;;
    minor)
        MINOR=$((MINOR + 1))
        PATCH=0
        NEW_VER="${MAJOR}.${MINOR}.${PATCH}"
        ;;
    major)
        MAJOR=$((MAJOR + 1))
        MINOR=0
        PATCH=0
        NEW_VER="${MAJOR}.${MINOR}.${PATCH}"
        ;;
    *.*.*)
        NEW_VER="$TYPE"
        IFS='.' read -r MAJOR MINOR PATCH <<< "$NEW_VER"
        ;;
    *)
        echo "Usage: ./bump.sh [patch|minor|major|<version>]"
        echo "Current version: $CURRENT_VER"
        exit 1
        ;;
esac

echo "Bumping version: $CURRENT_VER -> $NEW_VER"

# Replace version in csproj
sed -i "s|<Version>.*</Version>|<Version>$NEW_VER</Version>|" "$CSPROJ"

# Stage only the csproj file and commit
git add "$CSPROJ"
git commit -m "chore: bump version to $NEW_VER"

# Create full version tag (e.g. v1.1.0)
git tag -f -a "v$NEW_VER" -m "Release v$NEW_VER"
echo "Tagged v$NEW_VER"

# If patch is 0 (e.g. 1.1.0, 2.0.0), also create short version tag (e.g. v1.1, v2.0)
if [ "$PATCH" -eq 0 ]; then
    SHORT_TAG="v${MAJOR}.${MINOR}"
    git tag -f -a "$SHORT_TAG" -m "Release $SHORT_TAG"
    echo "Tagged $SHORT_TAG"
fi

echo "Done! Run to publish:"
echo "  git push --follow-tags"
