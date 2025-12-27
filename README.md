# JSONB-STORE
A high-performance, single-file application data format using C#, SQLite (Microsoft.Data.Sqlite), and Dapper.

# Core Architecture

## Primary Format
A single SQLite .db file acting as an "Application File Format".

## Data Storage Strategy
Treat SQLite as a hybrid relational/document store. Small JSON metadata files are stored as binary JSON (JSONB) or TEXT blobs to avoid filesystem overhead. High-frequency binary signal data is stored as BLOB columns for maximum throughput.

## Data Access Layer
Use Dapper for next-to-zero mapping overhead.

## Custom Logic
Implement a SqlMapper.TypeHandler<T> for Dapper to automatically handle JSON serialization/deserialization of C# objects into SQLite text/blob columns.

## Performance Requirements
- Minimize System Calls: The design must utilize SQLite's ability to be up to 35% faster than raw file I/O for small blobs by reducing open() and close() operations.
- Transaction Batching: All writes must be grouped into transactions to maintain high write speed.
- Modern SQLite Features: Utilize JSONB (SQLite 3.45+) for binary-optimized JSON storage to eliminate repetitive parsing overhead.

## Configuration
The library should default to WAL (Write-Ahead Logging) mode and synchronous = NORMAL for optimal balance between safety and performance.
