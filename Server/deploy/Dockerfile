# =============================================================================
# Goti Striker server — runtime-only image, for future Kubernetes / Cloud Run.
#
# NOT used by the current deployment. At present the server runs directly under
# systemd on a single e2-micro (see deploy-server.sh), which is the right call
# for ~10 concurrent users: no daemon overhead on a 1 GB box.
#
# This image exists so migrating to K8s later is a build step, not a rewrite.
#
# Unlike the previous Dockerfile, this does NOT compile anything. It packages
# the self-contained binary produced by deploy-server.sh, so the image is tiny
# and the build never needs the .NET SDK.
#
# Build:
#   cd Server/PitStrikerServer
#   dotnet publish -c Release -r linux-x64 --self-contained true \
#     -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o ./publish-linux
#   docker build -f ../deploy/Dockerfile -t gotistriker-server:latest .
#
# Run:
#   docker run -p 7777:7777 gotistriker-server:latest
# =============================================================================

# Self-contained binaries still need a libc. Alpine uses musl, so either publish
# with -r linux-musl-x64 or base on a glibc distro; debian-slim is the safe default.
FROM debian:12-slim

RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates libicu72 \
 && rm -rf /var/lib/apt/lists/*

# Run unprivileged — same posture as the systemd unit.
RUN useradd --system --no-create-home --shell /usr/sbin/nologin gotistriker
WORKDIR /app

COPY publish-linux/PitStrikerServer /app/GotiStrikerServer
COPY appsettings.json /app/appsettings.json
RUN chmod +x /app/GotiStrikerServer && chown -R gotistriker:gotistriker /app

USER gotistriker

ENV PORT=7777 \
    HOST=0.0.0.0 \
    ENVIRONMENT=Production \
    LOG_LEVEL=Info

EXPOSE 7777

# The server answers plain HTTP on the same port for health checks, which is
# what a K8s liveness probe would hit.
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD /app/GotiStrikerServer --health 2>/dev/null || exit 0

ENTRYPOINT ["/app/GotiStrikerServer"]
