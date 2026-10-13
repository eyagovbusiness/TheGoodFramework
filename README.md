# TheGoodFramework
This project is a set of personal libraries I developed to use in my other projects, it contains pattern implementations, code abstractions and extensions. The most remarkable part is a framework for developing web applications following Clean Architecture(CA) and Domain Driven Design(DDD), a customized Railway Oriented Programming(ROP) library and many other web application abstractions with a big impact on reducing the development time of my projects.
# BGS base-image migration status

The BGS image (`Dockerfile_bgs`, built by `local_deploy.sh --bgs` and
`make local-bgs`) is based on one immutable canonical Alpine image. The literal
`ARG ALPINE_BASE_IMAGE=<reference>` default in `Dockerfile_bgs` is the single
authoritative pin, and it is changed only through review. Do not duplicate it in
workflow variables or other files.

`scripts/validate_bgs_alpine_base_image.sh` resolves and validates the
reference before the project archive is created or Docker starts. When
`ALPINE_BASE_IMAGE` is unset it reads that committed default; when it is set,
even to an empty string, the supplied override must pass the same validation
and is never silently replaced. The reference must match
`host[:port]/base-images/alpine[:descriptive-tag]@sha256:<64-lowercase-hex>`;
tag-only references and `latest`, including `latest@sha256`, fail. The registry
host stays configurable through the reference itself. The non-BGS `Dockerfile`
and Jenkins pipelines are unchanged.

This draft deliberately retains legacy output routing. Before publishing, the
canonical main-only publication path and scanned TGF publisher migration must
be completed, and the change must be reviewed and merged.