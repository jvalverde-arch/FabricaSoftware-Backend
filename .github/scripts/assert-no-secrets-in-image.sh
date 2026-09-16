#!/usr/bin/env bash
# Falla si la imagen construida contiene configuración local o credenciales (estandar-backend.md §7).
#
# Inspecciona la imagen, no el .dockerignore: el .dockerignore es la intención, esto es el hecho. También atrapa el
# día que un COPY explícito, una capa heredada o un `dotnet publish` metan el archivo por otro camino.
#
# Uso: assert-no-secrets-in-image.sh <imagen[:etiqueta]>
set -euo pipefail

image="${1:?uso: assert-no-secrets-in-image.sh <imagen[:etiqueta]>}"

# Rutas prohibidas dentro de la imagen. Los archivos de ejemplo (.env.example, appsettings.Local.example.json) sí
# pueden viajar: no llevan valores.
forbidden='(^|/)appsettings\.[^/]*Local\.json$|(^|/)\.env(\.[^/]*)?$|(^|/)secrets\.json$|(^|/)id_rsa$|(^|/)\.npmrc$|(^|/)\.git-credentials$|\.(pfx|p12)$'
allowed='(\.example\.json$|\.env\.example$)'

container="$(docker create "$image")"
trap 'docker rm -f "$container" >/dev/null 2>&1 || true' EXIT

found="$(docker export "$container" | tar -t | grep -E "$forbidden" | grep -Ev "$allowed" || true)"

if [ -n "$found" ]; then
  echo "La imagen $image contiene archivos que nunca deben viajar dentro de ella:"
  printf '%s\n' "$found" | sed 's/^/  - /'
  echo "Revisa el .dockerignore del repo y los COPY del Dockerfile (estandar-backend.md §7)."
  exit 1
fi

echo "OK: $image no contiene configuración local ni credenciales."
