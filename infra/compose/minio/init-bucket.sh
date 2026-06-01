#!/bin/sh
# Bootstrap the private MinIO bucket used for document image objects.
# Runs after `minio` reports healthy.

set -eu

ROOT_USER=$(cat /run/secrets/minio_root_user)
ROOT_PASSWORD=$(cat /run/secrets/minio_root_password)
S3_ACCESS_KEY=$(cat "${S3_ACCESS_KEY_FILE:-/run/secrets/s3_access_key}")
S3_SECRET_KEY=$(cat "${S3_SECRET_KEY_FILE:-/run/secrets/s3_secret_key}")

ENDPOINT="${MINIO_ENDPOINT:-http://minio:9000}"
BUCKET="${MINIO_BUCKET:-advanced-rag-document-images}"

mc alias set local "${ENDPOINT}" "${ROOT_USER}" "${ROOT_PASSWORD}" --api S3v4 > /dev/null

# Create the main bucket if absent.
if ! mc ls "local/${BUCKET}" > /dev/null 2>&1; then
    mc mb "local/${BUCKET}"
fi

# Document images stay private. Browser access is authorized and streamed by dotnet-api.
mc anonymous set none "local/${BUCKET}" > /dev/null || true

cat > /tmp/document-images-rw.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"],
      "Resource": ["arn:aws:s3:::${BUCKET}/*"]
    },
    {
      "Effect": "Allow",
      "Action": ["s3:ListBucket"],
      "Resource": ["arn:aws:s3:::${BUCKET}"]
    }
  ]
}
EOF

mc admin user add local "${S3_ACCESS_KEY}" "${S3_SECRET_KEY}" > /dev/null || true
mc admin policy create local document-images-rw /tmp/document-images-rw.json > /dev/null || true
mc admin policy attach local document-images-rw --user "${S3_ACCESS_KEY}" > /dev/null

# Quiet: print the listing once for the operator log.
echo "MinIO bucket initialised:"
mc ls "local/"
echo "Bucket policy:"
mc anonymous get "local/${BUCKET}" || true
