# Setup

1. Create CF template using `buildtools/ci.template` in the account that runs the integration tests
2. Copy output `CodeBuildProjectName` & `OidcRole` output variables.
3. Create `CI_AWS_ROLE_ARN` repository secret with `OidcRole` value and
   `CI_AWS_CODE_BUILD_PROJECT_NAME` repository secret with `CodeBuildProjectName`
   value.
4. You must create the IAM role that represents `CI_AWS_ROLE_ARN`. The role only needs to have access to the AWS account
the CF template was created on.
4. Voila!

# Troubleshooting

## thumbprint rotation
```
Error: OpenIDConnect provider's HTTPS certificate doesn't match configured thumbprint
```

This can happen if GitHub has rotated the thumbprint of the certificate. Follow [this guide](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_providers_create_oidc_verify-thumbprint.html) to generate new thumbprint.

Redeploy the ci.template with the new thumbprint. Additionally, contact https://github.com/aws-actions/configure-aws-credentials/issues for the thumbprint rotation.
