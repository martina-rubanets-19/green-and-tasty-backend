#!/usr/bin/env bash
# Створює таблиці для групового pre-order (якщо в проєкті їх ще немає).
# Назви таблиць/індексів мають збігатися з Domain/Entities.cs.
set -euo pipefail
REGION="${AWS_REGION:-us-west-2}"

aws dynamodb create-table --region "$REGION" --table-name Reservations \
  --attribute-definitions AttributeName=id,AttributeType=S \
  --key-schema AttributeName=id,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST

aws dynamodb create-table --region "$REGION" --table-name GroupReservations \
  --attribute-definitions AttributeName=id,AttributeType=S \
  --key-schema AttributeName=id,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST

aws dynamodb create-table --region "$REGION" --table-name GuestSessions \
  --attribute-definitions \
      AttributeName=id,AttributeType=S \
      AttributeName=groupReservationId,AttributeType=S \
      AttributeName=reservationId,AttributeType=S \
  --key-schema AttributeName=id,KeyType=HASH \
  --global-secondary-indexes \
      'IndexName=groupReservationId-index,KeySchema=[{AttributeName=groupReservationId,KeyType=HASH}],Projection={ProjectionType=ALL}' \
      'IndexName=reservationId-index,KeySchema=[{AttributeName=reservationId,KeyType=HASH}],Projection={ProjectionType=ALL}' \
  --billing-mode PAY_PER_REQUEST

aws dynamodb create-table --region "$REGION" --table-name Orders \
  --attribute-definitions \
      AttributeName=id,AttributeType=S \
      AttributeName=reservationId,AttributeType=S \
  --key-schema AttributeName=id,KeyType=HASH \
  --global-secondary-indexes \
      'IndexName=reservationId-index,KeySchema=[{AttributeName=reservationId,KeyType=HASH}],Projection={ProjectionType=ALL}' \
  --billing-mode PAY_PER_REQUEST

for t in Reservations GroupReservations GuestSessions Orders; do
  aws dynamodb wait table-exists --region "$REGION" --table-name "$t"
done
echo "Tables are ready."
