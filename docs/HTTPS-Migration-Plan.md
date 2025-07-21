# ShopBoss v2 HTTPS Migration Plan

## Executive Summary
This document outlines a comprehensive plan to migrate ShopBoss v2 from HTTP to HTTPS, including all necessary configuration changes, certificate management, and potential issues with mitigation strategies.

## Current State Analysis

### HTTP Configuration Points Identified
1. **Production Configuration** (`appsettings.Production.json`)
   - Hardcoded HTTP URLs: `http://0.0.0.0:5000`
   - Kestrel endpoints configured for HTTP only

2. **Development Configuration** (`Properties/launchSettings.json`)
   - Multiple HTTP URLs configured for different profiles
   - Some profiles have HTTPS configured but not all

3. **SignalR Connections** (Multiple view files)
   - All SignalR hubs use relative URLs (`/hubs/status`, `/importProgress`)
   - No hardcoded HTTP/WS protocols found

4. **Cookie Configuration** (`Program.cs`)
   - Session cookies configured with HttpOnly but missing Secure flag
   - No explicit SameSite configuration

5. **HTTPS Redirection** (`Program.cs`)
   - UseHttpsRedirection() is present but only active in non-development
   - HSTS is configured but only for non-development

## Implementation Plan

### Phase 1: Certificate Management

#### Development Environment
```bash
# Generate development certificate
dotnet dev-certs https --trust
```

#### Production Environment (Internal Windows LAN)
**Self-Signed Certificate Strategy**
- Zero cost, full control
- Use internal hostname (e.g., `shopboss.company.local`)
- Deploy to all client machines via Group Policy
- No external dependencies
- 10-year validity period

**Certificate Generation Commands:**
```powershell
# Generate self-signed certificate (run on Windows server)
$cert = New-SelfSignedCertificate -DnsName "shopboss.company.local" -CertStoreLocation "cert:\LocalMachine\My" -NotAfter (Get-Date).AddYears(10)

# Export for deployment
Export-PfxCertificate -Cert $cert -FilePath "shopboss.pfx" -Password (ConvertTo-SecureString -String "YourSecurePassword123" -Force -AsPlainText)

# Export public key for client deployment
Export-Certificate -Cert $cert -FilePath "shopboss.crt"
```

**Client Machine Deployment:**
```powershell
# Install certificate on client machines (via Group Policy or manual)
Import-Certificate -FilePath "shopboss.crt" -CertStoreLocation "cert:\LocalMachine\Root"
```

### Phase 2: Configuration Changes

#### 1. Update appsettings.Production.json
```json
{
  "Urls": "https://0.0.0.0:5001;http://0.0.0.0:5000",
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://0.0.0.0:5001",
        "Certificate": {
          "Path": "shopboss.pfx",
          "Password": "YourSecurePassword123"
        }
      },
      "Http": {
        "Url": "http://0.0.0.0:5000"
      }
    },
    "Limits": {
      "MaxConcurrentConnections": 100,
      "MaxConcurrentUpgradedConnections": 100,
      "MaxRequestBodySize": 104857600,
      "RequestHeadersTimeout": "00:00:30",
      "KeepAliveTimeout": "00:02:00"
    }
  }
}
```

#### 2. Update Program.cs
```csharp
// Add before builder.Build()
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Strict;
    options.Secure = CookieSecurePolicy.Always; // Force HTTPS for cookies
});

// Update session configuration
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Add this
    options.Cookie.SameSite = SameSiteMode.Strict; // Add this
});

// Configure HTTPS redirection
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
    options.HttpsPort = 5001;
});

// Configure HSTS
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
    options.ExcludedHosts.Add("localhost");
    options.ExcludedHosts.Add("127.0.0.1");
});
```

#### 3. Update launchSettings.json for Development
```json
{
  "profiles": {
    "ShopBoss.Web": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:7121;http://localhost:5269",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

### Phase 3: SignalR and WebSocket Considerations

#### SignalR Configuration
No changes needed - SignalR automatically upgrades to WSS when served over HTTPS.

#### Client-Side Verification
All SignalR connections use relative URLs, which will automatically use the correct protocol.

### Phase 4: External Dependencies

#### CDN Resources
Current CDN resources already use HTTPS:
- Font Awesome: `https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.0.0/css/all.min.css`
- Bootstrap Icons: `https://cdn.jsdelivr.net/npm/bootstrap-icons@1.10.0/font/bootstrap-icons.css`
- SignalR: `https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/7.0.0/signalr.min.js`

No changes required.

## Potential Issues and Mitigation

### 1. Mixed Content Warnings
**Issue**: Browser blocks HTTP resources on HTTPS pages
**Mitigation**: All resources already use HTTPS or relative URLs

### 2. Certificate Trust Issues
**Issue**: Self-signed certificates cause browser warnings
**Mitigation**: 
- Development: Use `dotnet dev-certs https --trust`
- Production: Deploy certificate to all client machines' Trusted Root store
- Use Group Policy for automatic deployment across domain

### 3. Performance Impact
**Issue**: TLS handshake adds latency
**Mitigation**:
- Enable HTTP/2 in Kestrel
- Configure TLS 1.2/1.3 only
- Enable OCSP stapling

### 4. WebSocket Connection Issues
**Issue**: Some proxies/firewalls block WSS
**Mitigation**:
- SignalR has automatic fallback to long-polling
- Ensure firewall allows port 5001

### 5. Cookie Security
**Issue**: Existing HTTP cookies won't work with Secure flag
**Mitigation**:
- Clear cookies during migration
- Provide user notice about re-login

### 6. Windows Service Certificate Access
**Issue**: Certificate access from Windows Service
**Mitigation**:
- Use file-based certificate (shopboss.pfx) with proper ACLs
- Grant service account read permissions to certificate file
- Store certificate in application directory with restricted access

## Testing Plan

### 1. Development Environment Testing
- [ ] Generate and trust development certificate
- [ ] Update launchSettings.json
- [ ] Verify HTTPS redirect works
- [ ] Test all SignalR connections
- [ ] Verify no mixed content warnings
- [ ] Test cookie persistence

### 2. Internal LAN Testing
- [ ] Generate self-signed certificate
- [ ] Update appsettings.Production.json
- [ ] Deploy certificate to test machines
- [ ] Test HTTPS endpoints
- [ ] Verify SignalR over WSS
- [ ] Test with various browsers (Chrome, Edge, Firefox)
- [ ] Verify certificate trust on all client machines

### 3. Production Deployment
- [ ] Backup current configuration
- [ ] Generate production certificate
- [ ] Deploy certificate to all client machines
- [ ] Update DNS/hosts files if needed
- [ ] Deploy HTTPS configuration
- [ ] Monitor for errors
- [ ] Verify all functionality

### 4. Post-Deployment Verification
- [ ] Test certificate trust on all client machines
- [ ] Monitor TLS handshake performance
- [ ] User acceptance testing
- [ ] Document certificate renewal process (10-year validity)

## Rollback Plan

1. Keep HTTP endpoint active initially
2. Monitor HTTPS adoption
3. Maintain configuration backups
4. Document certificate installation process
5. Keep HTTP-only configuration ready

## Security Recommendations

1. **TLS Configuration**
   - Disable TLS 1.0/1.1
   - Use strong cipher suites
   - Enable perfect forward secrecy

2. **Headers**
   - Implement Content Security Policy
   - Add X-Frame-Options
   - Enable X-Content-Type-Options

3. **Certificate Management**
   - Document renewal process (10-year validity)
   - Monitor expiration dates
   - Keep certificate generation commands documented

## Timeline Estimate

- **Development Setup**: 1-2 hours
- **Configuration Updates**: 2-4 hours
- **Testing**: 4-8 hours
- **Production Deployment**: 2-4 hours
- **Total**: 1-2 days with buffer

## Conclusion

The migration to HTTPS is straightforward due to:
- No hardcoded HTTP URLs in application code
- SignalR using relative URLs
- Existing HTTPS redirection middleware
- Modern ASP.NET Core HTTPS support

Main effort will be in certificate management and testing across environments.