# Build-only entry point. Deliberately does not expose install/driver targets.
.DEFAULT_GOAL := firmware
SHELL := /bin/bash
override LOCAL_SRCS = src/tsf.c src/patch.c src/ucode_compressed.c
override COMMON_SRCS = $(addprefix $(NEXMON_ROOT)/patches/common/,wrapper.c objmem.c ucode_compression_code.c)
include ../nexmon/Makefile
.PHONY: firmware
firmware: $(RAM_FILE)

# Invoking 'make all' must also remain build-only in this variant. The inherited
# all rule has brcmfmac.ko as a prerequisite; neutralize that target below.
brcmfmac.ko:
	@echo "No driver is built by the DrcLink TSF prototype."
install-firmware backup-firmware:
	@echo "Deployment/backup is a separate operation; see DrcLink docs." >&2
	@exit 1
