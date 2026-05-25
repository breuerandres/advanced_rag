#!/bin/sh
# Bootstrap the MinIO bucket used by the help-center.
# Runs after `minio` reports healthy.

set -eu

ROOT_USER=$(cat /run/secrets/minio_root_user)
ROOT_PASSWORD=$(cat /run/secrets/minio_root_password)

# Configure the mc alias.
mc alias set local http://minio:9000 "${ROOT_USER}" "${ROOT_PASSWORD}" --api S3v4 > /dev/null

# Create the main bucket if absent.
if ! mc ls "local/${BUCKET}" > /dev/null 2>&1; then
    mc mb "local/${BUCKET}"
fi

# Public-read policy on tenant/brand/ (logo, favicon).
cat > /tmp/brand-public.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": "*",
      "Action": ["s3:GetObject"],
      "Resource": ["arn:aws:s3:::${BUCKET}/tenant/brand/*"]
    }
  ]
}
EOF
mc anonymous set-json /tmp/brand-public.json "local/${BUCKET}" > /dev/null || true

# Quiet: print the listing once for the operator log.
echo "MinIO bucket initialised:"
mc ls "local/"
echo "Bucket policy:"
mc anonymous get "local/${BUCKET}" || true
