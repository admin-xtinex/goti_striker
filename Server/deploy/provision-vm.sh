#!/usr/bin/env bash
# =============================================================================
# Goti Striker — one-time GCP VM provisioning (no Docker)
#
# Creates the firewall rule, reserves a static IP and creates the VM. The VM
# itself stays bare: no Docker, no .NET SDK, no git. Application code arrives
# later as a single self-contained binary via deploy-server.sh.
#
# Run once, from Google Cloud Shell (https://shell.cloud.google.com) or any
# machine with an authenticated gcloud CLI.
# =============================================================================
set -euo pipefail

# These names match what is already deployed, so re-running this script is a no-op
# instead of creating a parallel set of resources. The "pitstriker"/"pitstricker"
# spellings are the live ones and are deliberately NOT renamed to goti_striker:
# a GCP project ID is immutable, and renaming the VM/tag would orphan the firewall rule.
PROJECT_ID="${GCP_PROJECT_ID:-$(gcloud config get-value project 2>/dev/null)}"
REGION="${GCP_REGION:-us-central1}"
ZONE="${GCP_ZONE:-us-central1-a}"
INSTANCE="${GCP_INSTANCE:-pitstriker-server-01}"
IP_NAME="${GCP_IP_NAME:-pitstriker-static-ip}"
NET_TAG="${GCP_NET_TAG:-pitstriker-server}"
FW_RULE="${GCP_FW_RULE:-allow-pitstriker-ws}"

if [ -z "$PROJECT_ID" ]; then
  echo "ERROR: no project set. Run: gcloud config set project <PROJECT_ID>"
  exit 1
fi

echo "=============================================="
echo " Provisioning Goti Striker server VM"
echo "  project : $PROJECT_ID"
echo "  instance: $INSTANCE ($ZONE)"
echo "  machine : e2-micro (free tier)"
echo "=============================================="

# 1. Firewall for the WebSocket port
echo "[1/4] Firewall rule for tcp:7777..."
gcloud compute firewall-rules create "$FW_RULE" \
  --project="$PROJECT_ID" \
  --allow tcp:7777 \
  --target-tags="$NET_TAG" \
  --description="Goti Striker multiplayer WebSocket" \
  --quiet 2>/dev/null || echo "      already exists"

# 2. Static IP, so the DNS record never has to change
echo "[2/4] Reserving static IP ($IP_NAME)..."
gcloud compute addresses create "$IP_NAME" \
  --project="$PROJECT_ID" --region="$REGION" --quiet 2>/dev/null || echo "      already reserved"
STATIC_IP=$(gcloud compute addresses describe "$IP_NAME" \
  --project="$PROJECT_ID" --region="$REGION" --format='value(address)')
echo "      $STATIC_IP"

# 3. The VM. Deliberately bare — only unattended security upgrades.
#    Application code is shipped as a prebuilt binary, never compiled here.
echo "[3/4] Creating VM..."
gcloud compute instances create "$INSTANCE" \
  --project="$PROJECT_ID" \
  --zone="$ZONE" \
  --machine-type=e2-micro \
  --address="$STATIC_IP" \
  --tags="$NET_TAG" \
  --image-family=debian-12 \
  --image-project=debian-cloud \
  --boot-disk-size=10GB \
  --boot-disk-type=pd-standard \
  --metadata=startup-script='#!/bin/bash
apt-get update
apt-get install -y unattended-upgrades
# Nothing else is installed: the server ships as a self-contained binary.
' \
  --quiet 2>/dev/null || echo "      already exists"

# 4. Report
echo "[4/4] Done."
echo ""
echo "=============================================="
echo " Static IP : $STATIC_IP"
echo " WebSocket : ws://$STATIC_IP:7777"
echo ""
echo " DNS (xtinex.com):   A   pitstriker   ->  $STATIC_IP"
echo ""
echo " Next: ./deploy-server.sh   (ships the binary and starts the service)"
echo "=============================================="
