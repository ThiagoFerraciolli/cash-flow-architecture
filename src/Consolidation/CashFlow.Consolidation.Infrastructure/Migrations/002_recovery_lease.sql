ALTER TABLE projection_control ADD COLUMN maintenance_expires_at timestamptz;

-- Maintenance left by a pre-lease process must be eligible for takeover.
UPDATE projection_control
SET maintenance_run_id = COALESCE(maintenance_run_id, gen_random_uuid()),
    maintenance_started_at = COALESCE(maintenance_started_at, clock_timestamp()),
    maintenance_expires_at = clock_timestamp()
WHERE maintenance = true;

ALTER TABLE projection_control ADD CONSTRAINT projection_control_maintenance_lease_check
CHECK (maintenance = false OR
       (maintenance_run_id IS NOT NULL AND maintenance_started_at IS NOT NULL
        AND maintenance_expires_at IS NOT NULL));
