# Default values
NO_CACHE :=
DOCKERFILE := Dockerfile

# Define your environment
ENVIRONMENT := development

# Check if required environment variables are set
ifndef IMAGE_REGISTRY
$(error Required environment variable IMAGE_REGISTRY is not set)
endif

# Parse arguments
define parse_arguments
$(eval NO_CACHE := $(if $(findstring --no-cache,$(MAKECMDGOALS)),--no-cache))
$(eval DOCKERFILE := $(if $(findstring local-bgs,$(MAKECMDGOALS)),Dockerfile_bgs,Dockerfile))
endef

# Target to create tarball of project files
.PHONY: tarball
tarball:
	find . \( -name "*.csproj" -o -name "*.sln" -o -name "NuGet.docker.config" \) -print0 | tar -cvf projectfiles.tar --null -T -

# Target to build Docker image with or without cache
.PHONY: build
build:
	$(call parse_arguments)
	set -e; \
	if [ "$(DOCKERFILE)" = "Dockerfile_bgs" ]; then \
		base_image="$$(./scripts/validate_bgs_alpine_base_image.sh)"; \
		set -- --build-arg ALPINE_BASE_IMAGE="$$base_image"; \
		image_tag="$(IMAGE_REGISTRY)/base-images/the_good_framework:latest"; \
	else \
		set --; \
		image_tag="$(IMAGE_REGISTRY)/base-images/$(ENVIRONMENT)/the_good_framework:latest"; \
	fi; \
	$(MAKE) tarball; \
	docker build -f $(DOCKERFILE) . $(NO_CACHE) "$$@" --build-arg IMAGE_REGISTRY=$(IMAGE_REGISTRY) --build-arg ENVIRONMENT=$(ENVIRONMENT) -t "$$image_tag"

# Target to push Docker image to registry
.PHONY: push guard-push
# The canonical BGS image is published only by the approved main CI publisher.
guard-push:
	@if echo "$(MAKECMDGOALS)" | grep -qw local-bgs; then echo "Pushing the BGS image is not supported; canonical publication is done only by CI." >&2; exit 1; fi
push: guard-push build
	docker push $(IMAGE_REGISTRY)/base-images/$(ENVIRONMENT)/the_good_framework:latest

# Convenience targets
.PHONY: local local-bgs
local: build
local-bgs: build

# Clean up tarball
.PHONY: clean
clean:
	rm -f projectfiles.tar
