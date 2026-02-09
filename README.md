# WorkerTemplate

A basic C# Worker Service template designed for backend processing using **RabbitMQ** and **PostgreSQL**.

This project provides a simple starting point for building background workers that consume messages, process data, and store results in a database.

## Features

- ✅ PostgreSQL connection handling
- ✅ RabbitMQ connection handling
- ✅ Basic publish and subscribe messaging
- ✅ Message consumption and data insertion into PostgreSQL
- ✅ HMAC SHA256 hashing utility for signature verification and authentication
- ✅ Clean structure suitable for extension into production services

## Use Case

This template is suitable for:

- Background processing services
- Message-driven architectures
- Queue-based data processing
- Integration services between systems

## Project Flow

1. Worker connects to RabbitMQ.
2. Messages are published or consumed from configured queues.
3. Incoming messages are processed.
4. Data is inserted into PostgreSQL.
5. Optional signature verification using HMAC SHA256.

## Requirements

- .NET (Worker Service)
- PostgreSQL
- RabbitMQ

## Getting Started

1. Clone the repository
```bash
git clone https://github.com/khairul006/WorkerTemplate.git
```

2. Configure database and RabbitMQ settings in: `appsettings.json`

3. Run the worker service:
```bash
dotnet run
```

## Notes

This project is intended as a starter template, not a complete framework. Extend the structure based on your project requirements.