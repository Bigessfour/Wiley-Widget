# Docker Container Configuration Improvements

This document outlines the comprehensive improvements made to the Wiley Widget Docker container setup based on security, performance, and reliability best practices.

## 🚀 Quick Start

### Development Environment (Recommended)

```bash
# Start the enhanced development environment
docker-compose -f docker-compose.dev.yml up -d

# View logs
docker-compose -f docker-compose.dev.yml logs -f wiley-widget-dev

# Run tests
docker-compose -f docker-compose.dev.yml --profile testing up csx-test-runner
```

### Production-like Environment

```bash
# Start with production optimizations
docker-compose -f docker-compose.dev.yml -f docker-compose.prod-override.yml up -d
```

### Secure Environment (with secrets)

```bash
# Start with secrets management
docker-compose -f docker-compose.secure.yml up -d
```

## 🔧 Key Improvements Implemented

### 1. **Enhanced Health Checks**

- **Before**: Basic `dotnet script --version` check
- **After**: Comprehensive health verification with proper intervals, timeouts, and retry logic
- **Benefits**: Better container health monitoring and automatic recovery

### 2. **Resource Limits**

- **Memory Limits**: 512MB for dev, 1GB for production
- **CPU Limits**: 0.5 CPU cores for dev, 1.0 for production
- **Benefits**: Prevents resource exhaustion and ensures fair resource sharing

### 3. **Security Enhancements**

- **Non-root User**: Containers run as `csxuser` instead of root
- **Capability Dropping**: All capabilities dropped, only essential ones added
- **Read-only Filesystem**: Root filesystem mounted read-only with tmpfs for temp data
- **Security Options**: `no-new-privileges` and AppArmor profiles

### 4. **Logging Configuration**

- **Log Rotation**: 10MB max size, 3 files retained
- **Compression**: Enabled for production to save disk space
- **Benefits**: Prevents log files from consuming excessive disk space

### 5. **Restart Policies**

- **Development**: `unless-stopped` for persistent development sessions
- **Production**: `on-failure:3` for automatic recovery with limits
- **Testing**: `no` restart to prevent test interference

### 6. **Network Configuration**

- **Isolated Networks**: Custom bridge networks for better isolation
- **IPAM**: Configured subnet (172.20.0.0/16) for consistent addressing
- **Internal Networks**: Secure configurations with `internal: true` when needed

### 7. **Volume Management**

- **Read-only Mounts**: Source code mounted read-only where possible
- **Named Volumes**: NuGet cache uses named volumes for better persistence
- **tmpfs**: Temporary directories use memory-backed storage

### 8. **Graceful Shutdown**

- **Stop Timeout**: Increased to 30s for development, 15s for testing
- **Benefits**: Allows applications to shut down cleanly

### 9. **Secrets Management**

- **Docker Secrets**: Sensitive data handled via Docker secrets
- **File-based**: Secrets stored in `./secrets/` directory
- **Benefits**: Secure handling of credentials and API keys

## 📊 Configuration Files

### `docker-compose.dev.yml` - Enhanced Development

- Full development environment with live reloading
- Resource limits and security features
- Comprehensive health checks and logging

### `docker-compose.prod-override.yml` - Production Overrides

- Production-grade security and resource limits
- Enhanced logging with compression
- AppArmor and additional security options

### `docker-compose.secure.yml` - Secrets Management

- Demonstrates Docker secrets usage
- Isolated network for enhanced security
- Non-root user and read-only filesystem

## 🔍 Monitoring and Troubleshooting

### Health Checks

```bash
# Check container health
docker ps --filter "health=healthy"

# View health check logs
docker inspect wiley-widget-csx-dev | grep -A 10 "Health"
```

### Resource Usage

```bash
# Monitor resource usage
docker stats wiley-widget-csx-dev

# View resource limits
docker inspect wiley-widget-csx-dev | grep -A 10 "Limits"
```

### Logs

```bash
# View container logs
docker-compose -f docker-compose.dev.yml logs -f

# View specific service logs
docker-compose -f docker-compose.dev.yml logs -f wiley-widget-dev
```

## 🛡️ Security Features

### Container Security

- **Non-root execution**: All containers run as unprivileged user
- **Minimal capabilities**: Only essential Linux capabilities retained
- **Read-only root**: Prevents filesystem modifications
- **No new privileges**: Prevents privilege escalation

### Network Security

- **Isolated networks**: Containers communicate through defined networks only
- **Internal networks**: Secure configurations prevent external access
- **No exposed ports**: Development focus with controlled access

### Secrets Security

- **Docker secrets**: Sensitive data managed by Docker
- **File-based secrets**: Encrypted at rest
- **Runtime only**: Secrets only available at container runtime

## 🚀 Performance Optimizations

### Resource Management

- **Memory limits**: Prevent memory exhaustion
- **CPU limits**: Ensure fair CPU sharing
- **tmpfs mounts**: Fast temporary storage in memory

### Build Optimizations

- **Layer caching**: Optimized Dockerfile for better caching
- **Multi-stage builds**: Smaller final images
- **Package caching**: NuGet packages cached in named volumes

### Runtime Optimizations

- **Health checks**: Efficient health verification
- **Logging rotation**: Prevents log-induced performance issues
- **Graceful shutdown**: Clean application termination

## 📝 Usage Examples

### Development Workflow

```bash
# Start development environment
docker-compose -f docker-compose.dev.yml up -d

# Run CSX tests
docker-compose -f docker-compose.dev.yml --profile testing up csx-test-runner

# View test results
docker-compose -f docker-compose.dev.yml logs csx-test-runner

# Stop environment
docker-compose -f docker-compose.dev.yml down
```

### Production Deployment

```bash
# Deploy with production settings
docker-compose -f docker-compose.dev.yml -f docker-compose.prod-override.yml up -d

# Monitor health
docker ps --filter "health=healthy"

# Scale services if needed
docker-compose -f docker-compose.dev.yml up -d --scale wiley-widget-dev=2
```

### Secure Deployment

```bash
# Ensure secrets exist
ls -la ./secrets/

# Deploy with secrets
docker-compose -f docker-compose.secure.yml up -d

# Verify secrets are loaded
docker exec wiley-widget-secure env | grep -E "(GITHUB|DATABASE|API)"
```

## 🔧 Maintenance

### Cleanup

```bash
# Remove stopped containers
docker-compose -f docker-compose.dev.yml down

# Clean up unused resources
docker system prune -f

# Remove volumes (WARNING: destroys data)
docker volume prune -f
```

### Updates

```bash
# Pull latest base images
docker-compose -f docker-compose.dev.yml pull

# Rebuild images
docker-compose -f docker-compose.dev.yml build --no-cache

# Update and restart
docker-compose -f docker-compose.dev.yml up -d
```

## 📈 Monitoring

### Container Metrics

- Health status via `docker ps`
- Resource usage via `docker stats`
- Logs via `docker-compose logs`

### Application Metrics

- Custom health checks in application code
- Structured logging for better observability
- Performance monitoring via application frameworks

## 🤝 Contributing

When making changes to the Docker configuration:

1. Test all configurations locally
2. Update this documentation
3. Ensure security best practices are maintained
4. Validate resource limits are appropriate
5. Test health checks and restart policies

## 📚 Additional Resources

- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [Docker Security Best Practices](https://docs.docker.com/develop/dev-best-practices/security/)
- [Docker Health Checks](https://docs.docker.com/engine/reference/builder/#healthcheck)
- [Docker Secrets](https://docs.docker.com/engine/swarm/secrets/)
