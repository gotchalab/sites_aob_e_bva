# Contribuir para o AOB / BVA

Este projecto usa um **git flow simplificado**: `dev` como branch de
integração e `main` como estado sempre-igual-ao-que-esta-em-producao.
Cada passagem a producao e marcada com uma tag `vX.Y.Z`.

```
   feature/*  ─┐
   fix/*      ─┼──►  dev  ────►  main (== producao)  ──►  tag vX.Y.Z
   hotfix/*   ─┘             (via merge, no PR)
```

## Fluxo de trabalho

### Trabalho normal (features, fixes, refactors)

```bash
git checkout dev && git pull
git checkout -b feat/nome-descritivo   # ou fix/... , chore/...
# trabalhar, commitar
git push -u origin feat/nome-descritivo
# abrir PR feat/nome-descritivo -> dev
# merge quando estiver pronto (podes usar squash)
```

Alternativa para alteracoes pequenas e obvias: commit directo em `dev`.

### Passagem a producao (release / deploy)

Quando `dev` esta pronto para ir para a VPS:

```bash
git checkout main && git pull
git merge --no-ff dev -m "release: vX.Y.Z"
git tag -a vX.Y.Z -m "Descricao curta do que vai na release"
git push origin main --follow-tags

# deploy
cd infra/deploy
python deploy.py api admin aobarcelos bva
```

Convencao de versoes (semver):

- `vMAJOR.MINOR.PATCH`
- **MAJOR:** breaking change (nova arquitectura, migracao com data loss).
- **MINOR:** feature nova (novo tipo de formulario, novo endpoint, novo campo publico).
- **PATCH:** correccoes, ajustes de estilo, alteracoes de conteudo/copy.

Se nao tens a certeza da magnitude, aumenta o PATCH.

### Hotfix (correccao urgente directamente em producao)

Se `dev` tem trabalho por acabar e precisas de corrigir producao ja:

```bash
git checkout main && git pull
git checkout -b hotfix/descricao
# fix + commit
git checkout main && git merge --no-ff hotfix/descricao
git tag -a vX.Y.Z+1 -m "hotfix: descricao"
git push origin main --follow-tags

# nao esquecer de propagar para dev
git checkout dev && git merge main && git push
```

## Regras importantes

1. **`main` nunca recebe commits directos** — sempre via merge de `dev` ou de um branch `hotfix/*`.
2. **`main` == producao** — o que esta em `main` no ultimo push e o que corre na VPS. Se falhar depois de um merge para `main`, reverter e voltar a taggar.
3. **Deploy so a partir de `main`** — o `deploy.py` avisa se nao estiveres em `main` mas nao bloqueia (podes usar para testes pontuais em dev).
4. **Nunca `git push --force`** ao `main` ou `dev`. Feature branches podem ser rebased/force-pushed enquanto ainda sao teus (nao mergeados).
5. **Deploy fresh checkout** — antes de fazer deploy, `git status` deve estar limpo. O `deploy.py` compila do working tree, nao do commit — se tiveres alteracoes por commitar vao para producao sem ficar registadas.

## Estrutura de branches actual

- `main` — producao. Tag mais recente = versao actual em prod.
- `dev` — integracao. Onde tudo acontece antes de subir a prod.
- `feat/*`, `fix/*`, `chore/*`, `refactor/*` — branches efemeras (apagar apos merge).
- `hotfix/*` — para correccoes urgentes em prod que nao podem esperar pelo ciclo normal de `dev` → `main`.

## Mensagens de commit

Prefixos convencionais (ver `git log` para exemplos):

- `feat:` nova funcionalidade
- `fix:` correccao de bug
- `chore:` housekeeping (dependencias, config, reorganizacao)
- `refactor:` reestruturacao sem mudar comportamento
- `docs:` alteracoes so em documentacao/README
- `perf:` melhoria de performance
- `test:` adicao/alteracao de testes

Scope opcional entre parenteses: `fix(email): ...`, `feat(admin): ...`.
