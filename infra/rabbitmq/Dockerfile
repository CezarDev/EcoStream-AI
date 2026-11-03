# RabbitMQ + management UI + delayed message exchange plugin
# (used by MassTransit for delayed redelivery / scheduled messages).
# The plugin version must match the RabbitMQ minor version.
FROM rabbitmq:4.1-management

ARG DELAYED_EXCHANGE_VERSION=4.1.0
ADD --chown=rabbitmq:rabbitmq \
    https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/releases/download/v${DELAYED_EXCHANGE_VERSION}/rabbitmq_delayed_message_exchange-${DELAYED_EXCHANGE_VERSION}.ez \
    /opt/rabbitmq/plugins/

RUN rabbitmq-plugins enable --offline rabbitmq_delayed_message_exchange
