# 1. Record architecture decisions

Date: 2021-12-13

## Status

Accepted

## Context

We have two main hosting options for the Find My TRN service - GOV PaaS, or Azure CIP

## Decision

We will use the GOV PaaS platform to host the Find My TRN web application

## Consequences

* Cloud services are limited to that which are available on the GOV PaaS Platform
* Cloudfoundry to be used to operate platform
* AWS Shield ingress protection
* 24/7 Support available
