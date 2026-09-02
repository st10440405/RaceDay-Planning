# RaceDay System

## System Description
RaceDay is an event management system designed to streamline the organization of athletic events and races. It provides a platform where Organisers can create and manage events, and Participants can enroll in various categories associated with those events. The system ensures robust data integrity and offers a seamless registration experience for all users.

## Roles
*   **Organiser**: Responsible for managing events. Organisers can create new events, define categories within those events, track participant enrolments, and capture participant results.
*   **Participant**: Individuals who wish to take part in events. Participants can browse available events, register for specific categories, view their enrolment status, and track their personal results.

## CI/CD Pipeline
The project utilizes GitHub Actions for Continuous Integration. The workflow validates that the required planning documents are present in the repository.

![CI/CD Success Status](./docs/Screenshot%202026-09-02%20082111.png)

## Video Presentation
[RaceDay System Planning Presentation](https://youtu.be/OZTfFIFFazQ)

## Project Setup
1. Clone the repository: `git clone https://github.com/st10440405/RaceDay-Planning.git`
2. Open SQL Server Management Studio (SSMS).
3. Open the `docs/RaceDay_Schema.sql` file.
4. Execute the script to create the database, tables, and seed data.

## License
MIT.