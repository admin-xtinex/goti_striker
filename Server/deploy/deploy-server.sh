#!/usr/bin/env bash
# =============================================================================
# Goti Striker — deploy the multiplayer server (no Docker)
#
# Publishes a self-contained single-file Linux binary on THIS machine, ships it
# to the VM, and restarts a systemd service. Nothing is compiled on the VM:
# no SDK, no runtime, no Docker daemon. The whole artifact is one ~39 MB file,
# which matters on a 1 GB e2-micro.
#
# Usage:   ./deploy-server.sh
# Needs:   gcloud CLI authenticated, dotnet SDK 10
# =============================================================================
set -euo pipefail

# NOTE: the GCP project is "pitstricker" — the original misspelling. It is the real
# project ID and cannot be renamed, so it stays as-is even though the repo is goti_striker.
PROJECT_ID="${GCP_PROJECT_ID:-pitstricker}"
ZONE="${GCP_ZONE:-us-central1-a}"
INSTANCE="${GCP_INSTANCE:-pitstriker-server-01}"
REMOTE_DIR="/opt/gotistriker"
SERVICE="gotistriker-server"
BINARY="GotiStrikerServer"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVER_DIR="$SCRIPT_DIR/../PitStrikerServer"
STAGE="$SCRIPT_DIR/.stage"

echo "=============================================="
echo " Deploying Goti Striker server"
echo "  project : $PROJECT_ID"
echo "  instance: $INSTANCE ($ZONE)"
echo "=============================================="

command -v gcloud >/dev/null || { echo "ERROR: gcloud CLI not found. https://cloud.google.com/sdk/docs/install"; exit 1; }
command -v dotnet >/dev/null || { echo "ERROR: dotnet SDK not found."; exit 1; }

# ---- 1. build ---------------------------------------------------------------
echo "[1/5] Publishing self-contained linux-x64 binary..."
rm -rf "$STAGE"
mkdir -p "$STAGE"
dotnet publish "$SERVER_DIR/PitStrikerServer.csproj" \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
  -o "$STAGE/out" --nologo

mv "$STAGE/out/PitStrikerServer" "$STAGE/out/$BINARY"
rm -f "$STAGE/out/"*.pdb
cp "$SERVER_DIR/appsettings.json" "$STAGE/out/" 2>/dev/null || true
cp "$SCRIPT_DIR/$SERVICE.service" "$STAGE/out/"
echo "      artifact: $(du -h "$STAGE/out/$BINARY" | cut -f1)"

# ---- 2. upload --------------------------------------------------------------
echo "[2/5] Uploading to $INSTANCE..."
gcloud compute scp --project "$PROJECT_ID" --zone "$ZONE" --recurse \
  "$STAGE/out"/* "$INSTANCE:/tmp/gotistriker-deploy/" --quiet 2>/dev/null \
  || {
    gcloud compute ssh "$INSTANCE" --project "$PROJECT_ID" --zone "$ZONE" --quiet \
      --command "mkdir -p /tmp/gotistriker-deploy"
    gcloud compute scp --project "$PROJECT_ID" --zone "$ZONE" \
      "$STAGE/out"/* "$INSTANCE:/tmp/gotistriker-deploy/" --quiet
  }

# ---- 3-5. install + restart on the VM ---------------------------------------
echo "[3/5] Installing and restarting service..."
gcloud compute ssh "$INSTANCE" --project "$PROJECT_ID" --zone "$ZONE" --quiet --command "
  set -e
  # dedicated unprivileged service account
  id -u gotistriker >/dev/null 2>&1 || sudo useradd --system --no-create-home --shell /usr/sbin/nologin gotistriker
  sudo mkdir -p $REMOTE_DIR

  # stop before replacing the binary so the file isn't busy
  sudo systemctl stop $SERVICE 2>/dev/null || true

  sudo cp /tmp/gotistriker-deploy/$BINARY $REMOTE_DIR/
  sudo cp /tmp/gotistriker-deploy/appsettings.json $REMOTE_DIR/ 2>/dev/null || true
  sudo chmod +x $REMOTE_DIR/$BINARY
  sudo chown -R gotistriker:gotistriker $REMOTE_DIR

  sudo cp /tmp/gotistriker-deploy/$SERVICE.service /etc/systemd/system/
  sudo systemctl daemon-reload
  sudo systemctl enable $SERVICE
  sudo systemctl restart $SERVICE
  rm -rf /tmp/gotistriker-deploy

  sleep 3
  sudo systemctl is-active $SERVICE
  sudo journalctl -u $SERVICE -n 20 --no-pager
"

rm -rf "$STAGE"
echo "=============================================="
echo " Deployed. Verify from anywhere:"
echo "   curl http://pitstriker.xtinex.com:7777/    (health endpoint)"
echo " Follow logs:"
echo "   gcloud compute ssh $INSTANCE --zone $ZONE --command 'sudo journalctl -u $SERVICE -f'"
echo "=============================================="
