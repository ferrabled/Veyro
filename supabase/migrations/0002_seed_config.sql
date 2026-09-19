-- Plausibility bounds for the shipping content version. Values are deliberately generous
-- first guesses (better to flag nothing honest than reject real runs); tighten from real
-- flagged-run data via a plain UPDATE — no redeploy, no migration.
--
-- max_speed_mps: forward speed ceiling with slack. Tune against TrackConfig once frozen.
-- max_coins_per_s: theoretical coin-line density ceiling with slack.

insert into public.game_config
  (content_version, max_speed_mps, max_coins_per_s, min_duration_s, max_duration_s, worlds)
values
  ('greybox-1', 30.0, 6.0, 3, 3600, array['greybox'])
on conflict (content_version) do nothing;
