# test-aspire

This repository demonstrates an [Aspire](https://learn.microsoft.com/aspire) hosted 
project setup that provides **consistent, memorable DNS names** for local development 
across multiple sites and subdomains.

## What It Does
The `AppConsole` project automates the creation and configuration of a local development 
environment by:

1. **Generating a certificate chain**  
   - Creates a root CA, intermediate certificates, and server certificates.  
   - Installs them into the appropriate Windows certificate stores to ensure 
     HTTPS works seamlessly in local browsers and applications.

2. **Configuring custom DNS entries**  
   The following host entries are added automatically:

```
127.0.0.2 site.test  
127.0.0.2 www.site.test  
127.0.0.3 api.site.test  
127.0.0.2 sub.site.test  
127.0.0.2 sub2.site.test  
```

3. **Simplifying Aspire dashboard URLs**  
Instead of dealing with `https://localhost:<random-port>`, you get clean, 
predictable, and easy-to-remember URLs such as:  
- https://www.site.test  
- https://api.site.test 
- https://sub.site.test  

## Why This Matters
When working on complex projects, especially multi-tenant applications or systems 
with multiple subdomains, having proper DNS setup in local development is critical.  
This setup enables developers to:

- **Mirror real-world environments** more accurately, using subdomains that match 
production (e.g., `api.` vs. `www.`).  
- **Test multi-tenant behavior**, where different tenants or customers may be 
distinguished by their subdomain or full domain.  
- **Validate domain-based features** such as custom branding, feature flags, or 
authentication flows tied to a specific hostname.  
- **Improve developer experience** by removing friction from random ports and 
mismatched URLs.  

## Example Use Cases
- **Front-end testing**: Verifying that styling or layout changes apply correctly 
depending on the subdomain (e.g. different themes for `sub.site.test` vs. 
`sub2.site.test`).  
- **API development**: Running and testing APIs on a domain (`api.site.test`) 
that mimics production routing.  
- **Authentication flows**: Debugging OAuth or cookie-domain handling, which often 
require consistent HTTPS domains and subdomains.  
- **Integration testing**: Running automated tests against predictable, 
certificate-backed domains in local environments.  

---

With this setup, your local development environment feels much closer to production, 
making it easier to catch issues early and build confidence in domain- and 
subdomain-based application logic.