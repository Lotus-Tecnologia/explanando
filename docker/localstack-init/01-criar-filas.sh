#!/bin/bash
# Cria as filas de varredura no LocalStack assim que ele fica pronto.
# Espelha o que sera provisionado no AWS SQS real.
set -euo pipefail

REGIAO="sa-east-1"

# Dead Letter Queue primeiro: a fila principal precisa do ARN dela na RedrivePolicy.
awslocal sqs create-queue \
  --region "$REGIAO" \
  --queue-name explanando-varreduras-dlq

ARN_DLQ=$(awslocal sqs get-queue-attributes \
  --region "$REGIAO" \
  --queue-url http://localhost:4566/000000000000/explanando-varreduras-dlq \
  --attribute-names QueueArn \
  --query 'Attributes.QueueArn' --output text)

# maxReceiveCount 5: depois de 5 tentativas frustradas a mensagem sai do caminho
# principal em vez de travar a fila. Investigacao manual acontece na DLQ.
awslocal sqs create-queue \
  --region "$REGIAO" \
  --queue-name explanando-varreduras \
  --attributes "{
    \"VisibilityTimeout\": \"300\",
    \"MessageRetentionPeriod\": \"1209600\",
    \"RedrivePolicy\": \"{\\\"deadLetterTargetArn\\\":\\\"${ARN_DLQ}\\\",\\\"maxReceiveCount\\\":\\\"5\\\"}\"
  }"

echo "Filas de varredura criadas."
