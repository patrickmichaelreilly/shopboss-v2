# ShopBoss Clean Deployment Guide

## Critical Windows Server Deployment Steps

This guide ensures a completely clean ShopBoss installation that initializes the database properly and avoids common deployment issues.

## Pre-Deployment Checklist

### 1. Complete Environment Cleanup

# Stop and remove any existing ShopBoss service
sc stop ShopBoss
sc delete ShopBoss

# Remove all existing installation files
rmdir /s /q "C:\ShopBoss"

# Clean any residual database files
del "C:\ShopBoss\shopboss.db*" /f /q 2>nul

# Navigate to project scripts directory
cd [project-root]\scripts

# Run clean installation (will remove existing files/service)
install-shopboss.bat -Force

# IMPORTANT: The service should now work correctly due to Data Protection key ring fix
# Import and tree loading will work properly when running as Windows Service

## Service Management

# Start service
sc start ShopBoss

# Stop service  
sc stop ShopBoss

# Check status
sc query ShopBoss

# View service configuration
sc qc ShopBoss