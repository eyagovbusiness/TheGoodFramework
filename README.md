# TheGoodFramework
This project is a set of personal libraries I developed to use in my other projects, it contains pattern implementations, code abstractions and extensions. The most remarkable part is a framework for developing web applications following Clean Architecture(CA) and Domain Driven Design(DDD), a customized Railway Oriented Programming(ROP) library and many other web application abstractions with a big impact on reducing the development time of my projects.
# BGS base-image migration status

`local_deploy.sh --bgs` and `make local-bgs` require `ALPINE_BASE_IMAGE` before
they archive sources or start Docker. It must be an immutable canonical
`host[:port]/base-images/alpine[:descriptive-tag]@sha256:<64-lowercase-hex>`
reference. Tag-only references and `latest`, including `latest@sha256`, fail.

Set repository variable `ALPINE_BASE_IMAGE` for the authenticated CI builds.
This draft deliberately retains legacy output routing; canonical main-only
publication and scan coverage remain pre-merge blockers until the real
finalized canonical image reference is available.
