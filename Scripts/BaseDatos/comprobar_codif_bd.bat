"C:\Program Files\PostgreSQL\17\bin\psql.exe" ^
-h localhost ^
-p 5433 ^
-U postgres ^
-d repredisl_api ^
-c "SHOW server_encoding; SHOW client_encoding;"