# ShopBoss Checkpoints

This directory contains known-good state checkpoints for development and testing.

## Checkpoint Structure

Each checkpoint should contain:
- `shopboss.db` - Database backup at known-good state
- `description.txt` - What this checkpoint represents
- `timestamp.txt` - When this checkpoint was created

## How to Use Checkpoints

### Create a Checkpoint
```bash
# Copy current database to checkpoint
cp src/ShopBoss.Web/shopboss.db checkpoints/[checkpoint-name]/shopboss.db

# Add description
echo "Description of this checkpoint" > checkpoints/[checkpoint-name]/description.txt

# Add timestamp
date > checkpoints/[checkpoint-name]/timestamp.txt
```

### Restore from Checkpoint
```bash
# Stop ShopBoss first
# Then restore database
cp checkpoints/[checkpoint-name]/shopboss.db src/ShopBoss.Web/shopboss.db
```

## Recommended Checkpoints

- `fresh-install` - Clean database right after first run
- `with-racks` - Database with storage racks configured
- `sample-workorder` - Database with a sample work order imported
- `beta-ready` - Database ready for beta testing

## Checkpoint Naming Convention

Use descriptive names that indicate the state:
- `fresh-install-v1.0`
- `after-phase-d3-implementation`
- `before-major-migration`
- `beta-deployment-ready`