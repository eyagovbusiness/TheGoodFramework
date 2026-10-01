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
	@if [ "$(DOCKERFILE)" = "Dockerfile_bgs" ]; then ./scripts/validate_bgs_alpine_base_image.sh; fi
	$(MAKE) tarball
	docker build -f $(DOCKERFILE) . $(NO_CACHE) --build-arg ALPINE_BASE_IMAGE="$(ALPINE_BASE_IMAGE)" --build-arg IMAGE_REGISTRY=$(IMAGE_REGISTRY) --build-arg ENVIRONMENT=$(ENVIRONMENT) -t $(IMAGE_REGISTRY)/base-images/$(ENVIRONMENT)/the_good_framework:latest

# Target to push Docker image to registry
.PHONY: push
push: build
	docker push $(IMAGE_REGISTRY)/base-images/$(ENVIRONMENT)/the_good_framework:latest

# Convenience targets
.PHONY: local local-bgs
local: build
local-bgs: build

# Clean up tarball
.PHONY: clean
clean:
	rm -f projectfiles.tar
