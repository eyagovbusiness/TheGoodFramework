# TheGoodFramework

Reusable .NET libraries for Clean Architecture, DDD, railway-oriented results,
web APIs, persistence, messaging, identity and licensing. The BGS image provides
the built framework packages under `/app/BasePackages` for downstream image builds.

## BGS image

`Dockerfile_bgs` is the maintained image recipe. Its literal `ALPINE_BASE_IMAGE`
default pins the internal Biognosys Alpine image by immutable digest. The validator
rejects floating tags, `latest`, malformed references and empty overrides before
building. CI always uses the reviewed committed pin.

Maintained image consumers are OF.Common (`main` and `dev`), then transitively
OF.Manager and OF.Orchestrator on their own maintained lines. Application deployment
environments remain separate from TGF source branches. The former non-BGS
`Dockerfile` and GuildSwarm `Jenkinsfile` are deprecated, unused historical files;
the supported delivery route is BGS GitHub Actions only.

### Build locally

From a clean checkout of reviewed `main`, use Bash, Python 3.11+, Git, Docker with
Buildx and access to the private registry. Authenticate interactively or with an
approved credential helper; never put a password in a command or source file.

```bash
docker login omicsflowinternal.azurecr.io
export IMAGE_REGISTRY=omicsflowinternal.azurecr.io
bash ./local_deploy.sh local-check --no-cache --bgs
# Convenience build (local latest tag only):
make local-bgs
```

Local BGS builds cannot push. Only the guarded main workflow publishes. The old
default non-BGS local/Makefile routes are unsupported; explicitly select `--bgs`
or `local-bgs`.

### Pull and consume

Find the full verified digest in a reviewed adoption PR or the annotated
`bgs-image/tgf-<run>.<attempt>` Git tag. Authenticate to the registry, then:

```bash
# Replace <64-hex> with the approved manifest digest, not a config/image id.
docker pull omicsflowinternal.azurecr.io/base-images/the_good_framework@sha256:<64-hex>
```

```dockerfile
FROM omicsflowinternal.azurecr.io/base-images/the_good_framework@sha256:<64-hex> AS base
```

The unique image build tag is `tgf-<run_number>.<run_attempt>`; `latest` is for
discovery only. Builds scan the exact image and verify the registry manifest and
config before release. Scan/publication and Git-release mapping artifacts are
attached to the main workflow run. Only a successful protected Git release permits
the digest notification that opens a reviewed Common pin PR.

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) for the reviewed short-lived PR workflow,
releases, hotfixes, retention, rollback and required GitHub protections.
